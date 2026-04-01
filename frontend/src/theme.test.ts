import { describe, expect, it } from "vitest";
import { attendance } from "./theme";

/** Parse a hex color (#RGB or #RRGGBB) or rgba string to [r, g, b] in 0-255. */
function parseColor(color: string): [number, number, number] {
  if (color.startsWith("#")) {
    let hex = color.slice(1);
    if (hex.length === 3) hex = hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
    return [
      parseInt(hex.slice(0, 2), 16),
      parseInt(hex.slice(2, 4), 16),
      parseInt(hex.slice(4, 6), 16),
    ];
  }
  const match = color.match(/rgba?\(\s*([\d.]+),\s*([\d.]+),\s*([\d.]+)/);
  if (!match) throw new Error(`Cannot parse color: ${color}`);
  return [Number(match[1]), Number(match[2]), Number(match[3])];
}

/** WCAG 2.x relative luminance. */
function relativeLuminance([r, g, b]: [number, number, number]): number {
  const [rs, gs, bs] = [r, g, b].map((c) => {
    const s = c / 255;
    return s <= 0.04045 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * rs + 0.7152 * gs + 0.0722 * bs;
}

/** WCAG contrast ratio between two colors. */
function contrastRatio(a: string, b: string): number {
  const la = relativeLuminance(parseColor(a));
  const lb = relativeLuminance(parseColor(b));
  const lighter = Math.max(la, lb);
  const darker = Math.min(la, lb);
  return (lighter + 0.05) / (darker + 0.05);
}

describe("attendance chip color contrast (WCAG 1.4.3)", () => {
  const entries = Object.entries(attendance) as [string, { bg: string; text: string }][];

  it.each(entries)(
    "%s meets 4.5:1 contrast ratio",
    (_name, { bg, text }) => {
      const ratio = contrastRatio(bg, text);
      expect(ratio).toBeGreaterThanOrEqual(4.5);
    },
  );
});
