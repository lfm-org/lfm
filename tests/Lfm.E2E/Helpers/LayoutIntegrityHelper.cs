// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Globalization;
using System.Text;
using Microsoft.Playwright;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Lfm.E2E.Helpers;

/// <summary>
/// Browser-computed layout checks for overlap and reflow regressions that axe
/// cannot detect. The detector intentionally checks user-facing and semantic
/// surfaces, not every internal node in a component tree.
/// </summary>
public static class LayoutIntegrityHelper
{
    public static async Task AssertNoOverlapsAsync(
        IPage page,
        ITestOutputHelper output,
        string? context = null)
    {
        var result = await page.EvaluateAsync<LayoutIntegrityResult>(AuditScript);
        if (result.Issues.Length == 0)
            return;

        var message = BuildMessage(result, context ?? page.Url);
        output.WriteLine(message);
        throw new XunitException(message);
    }

    private static string BuildMessage(LayoutIntegrityResult result, string context)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture,
            $"Layout integrity failed for {context}: {result.Issues.Length} issue(s)");
        builder.AppendLine(CultureInfo.InvariantCulture,
            $"Viewport: {result.ViewportWidth:0}x{result.ViewportHeight:0}, document: {result.DocumentWidth:0}x{result.DocumentHeight:0}");

        foreach (var issue in result.Issues)
        {
            if (issue.Kind == "horizontal-overflow")
            {
                builder.AppendLine(CultureInfo.InvariantCulture,
                    $"- horizontal overflow: document width {result.DocumentWidth:0}px exceeds viewport {result.ViewportWidth:0}px");
                continue;
            }

            var first = issue.First;
            var second = issue.Second;
            if (first is null || second is null)
                continue;

            builder.AppendLine(CultureInfo.InvariantCulture,
                $"- overlap {issue.Area:0.#}px2 at ({issue.SampleX:0.#}, {issue.SampleY:0.#}); top hit: {issue.TopHit}");
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"  first:  {first.Selector} \"{first.Text}\" {Format(first.Rect)} position={first.Position} z-index={first.ZIndex}");
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"  second: {second.Selector} \"{second.Text}\" {Format(second.Rect)} position={second.Position} z-index={second.ZIndex}");
        }

        return builder.ToString();
    }

    private static string Format(LayoutRect rect)
        => string.Create(CultureInfo.InvariantCulture,
            $"x={rect.X:0.#}, y={rect.Y:0.#}, w={rect.Width:0.#}, h={rect.Height:0.#}");

    private const string AuditScript =
        """
        () => {
          const overlapTolerance = 2;
          const issues = [];
          const selector = [
            'a[href]',
            'button',
            'input:not([type="hidden"])',
            'select',
            'textarea',
            '[role]',
            '[tabindex]:not([tabindex="-1"])',
            '[data-testid]',
            'h1',
            'h2',
            'h3',
            'h4',
            'h5',
            'h6',
            'label',
            'p',
            'li',
            'th',
            'td',
            'fluent-anchor',
            'fluent-button',
            'fluent-card',
            'fluent-label',
            'fluent-select',
            'fluent-text-area',
            'fluent-text-field'
          ].join(',');

          const ignoredSelector = [
            'script',
            'style',
            'template',
            'svg',
            'path',
            '[hidden]',
            '[aria-hidden="true"]',
            '[data-layout-integrity-ignore]'
          ].join(',');

          const viewportWidth = document.documentElement.clientWidth;
          const viewportHeight = document.documentElement.clientHeight;
          const documentWidth = Math.max(
            document.documentElement.scrollWidth,
            document.body ? document.body.scrollWidth : 0);
          const documentHeight = Math.max(
            document.documentElement.scrollHeight,
            document.body ? document.body.scrollHeight : 0);

          if (documentWidth > viewportWidth + overlapTolerance) {
            issues.push({
              kind: 'horizontal-overflow',
              area: documentWidth - viewportWidth,
              sampleX: viewportWidth,
              sampleY: 0,
              topHit: 'document',
              first: null,
              second: null
            });
          }

          const elements = Array.from(document.querySelectorAll(selector));
          const candidates = elements
            .filter(el => !el.closest(ignoredSelector))
            .map((el, index) => toCandidate(el, index))
            .filter(Boolean);

          for (let i = 0; i < candidates.length; i += 1) {
            for (let j = i + 1; j < candidates.length; j += 1) {
              const first = candidates[i];
              const second = candidates[j];
              if (first.el.contains(second.el) || second.el.contains(first.el)) {
                continue;
              }

              const intersection = intersect(first.rect, second.rect);
              if (!intersection) {
                continue;
              }

              const hit = hitTestPair(first, second, intersection, candidates);
              if (!hit) {
                continue;
              }

              outline(first.el, '#d13438');
              outline(second.el, '#0078d4');
              issues.push({
                kind: 'overlap',
                area: intersection.width * intersection.height,
                sampleX: hit.x,
                sampleY: hit.y,
                topHit: hit.topHit,
                first: serializable(first),
                second: serializable(second)
              });
            }
          }

          return {
            viewportWidth,
            viewportHeight,
            documentWidth,
            documentHeight,
            issues
          };

          function toCandidate(el, index) {
            const style = getComputedStyle(el);
            if (style.display === 'none' ||
                style.visibility === 'hidden' ||
                Number(style.opacity) === 0 ||
                style.pointerEvents === 'none') {
              return null;
            }

            const rect = el.getBoundingClientRect();
            if (rect.width < 2 || rect.height < 2) {
              return null;
            }
            if (rect.bottom < 0 || rect.right < 0 ||
                rect.top > window.innerHeight || rect.left > window.innerWidth) {
              return null;
            }

            return {
              el,
              index,
              selector: describe(el),
              text: textOf(el),
              position: style.position,
              zIndex: style.zIndex,
              rect: {
                x: rect.x,
                y: rect.y,
                width: rect.width,
                height: rect.height
              }
            };
          }

          function intersect(a, b) {
            const left = Math.max(a.x, b.x);
            const top = Math.max(a.y, b.y);
            const right = Math.min(a.x + a.width, b.x + b.width);
            const bottom = Math.min(a.y + a.height, b.y + b.height);
            const width = right - left;
            const height = bottom - top;
            if (width <= overlapTolerance || height <= overlapTolerance) {
              return null;
            }
            return { left, top, right, bottom, width, height };
          }

          function hitTestPair(first, second, intersection, allCandidates) {
            const points = [
              { x: intersection.left + intersection.width / 2, y: intersection.top + intersection.height / 2 },
              { x: intersection.left + 1, y: intersection.top + 1 },
              { x: intersection.right - 1, y: intersection.top + 1 },
              { x: intersection.left + 1, y: intersection.bottom - 1 },
              { x: intersection.right - 1, y: intersection.bottom - 1 }
            ];

            for (const point of points) {
              if (point.x < 0 || point.y < 0 ||
                  point.x > window.innerWidth || point.y > window.innerHeight) {
                continue;
              }

              const owners = [];
              for (const hitElement of document.elementsFromPoint(point.x, point.y)) {
                const owner = allCandidates.find(candidate =>
                  candidate.el === hitElement || candidate.el.contains(hitElement));
                if (owner && !owners.includes(owner)) {
                  owners.push(owner);
                }
              }

              if (owners.includes(first) && owners.includes(second)) {
                return {
                  x: point.x,
                  y: point.y,
                  topHit: owners[0] ? owners[0].selector : 'unknown'
                };
              }
            }

            return null;
          }

          function describe(el) {
            if (el.id) {
              return '#' + CSS.escape(el.id);
            }

            const testId = el.getAttribute('data-testid');
            if (testId) {
              return '[data-testid="' + testId.replaceAll('"', '\\"') + '"]';
            }

            const role = el.getAttribute('role');
            const tag = el.tagName.toLowerCase();
            if (role) {
              return tag + '[role="' + role.replaceAll('"', '\\"') + '"]';
            }

            return tag + ':nth-of-type(' + nthOfType(el) + ')';
          }

          function nthOfType(el) {
            let index = 1;
            let sibling = el;
            while ((sibling = sibling.previousElementSibling) !== null) {
              if (sibling.tagName === el.tagName) {
                index += 1;
              }
            }
            return index;
          }

          function textOf(el) {
            return (el.innerText || el.getAttribute('aria-label') || el.textContent || '')
              .replace(/\s+/g, ' ')
              .trim()
              .slice(0, 80);
          }

          function outline(el, color) {
            el.style.outline = '3px solid ' + color;
            el.style.outlineOffset = '-3px';
          }

          function serializable(candidate) {
            return {
              selector: candidate.selector,
              text: candidate.text,
              position: candidate.position,
              zIndex: candidate.zIndex,
              rect: candidate.rect
            };
          }
        }
        """;

    private sealed class LayoutIntegrityResult
    {
        public double ViewportWidth { get; set; }
        public double ViewportHeight { get; set; }
        public double DocumentWidth { get; set; }
        public double DocumentHeight { get; set; }
        public LayoutIntegrityIssue[] Issues { get; set; } = [];
    }

    private sealed class LayoutIntegrityIssue
    {
        public string Kind { get; set; } = string.Empty;
        public double Area { get; set; }
        public double SampleX { get; set; }
        public double SampleY { get; set; }
        public string TopHit { get; set; } = string.Empty;
        public LayoutElement? First { get; set; }
        public LayoutElement? Second { get; set; }
    }

    private sealed class LayoutElement
    {
        public string Selector { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string ZIndex { get; set; } = string.Empty;
        public LayoutRect Rect { get; set; } = new();
    }

    private sealed class LayoutRect
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
