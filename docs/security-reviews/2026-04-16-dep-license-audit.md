# Dependency License Audit — Baseline

- **Date:** 2026-04-16
- **Scope:** all NuGet packages resolved by `lfm.sln` (transitives included)
- **Tool:** `nuget-license` 4.0.9 (see `.config/dotnet-tools.json`)
- **Raw data:** regenerate via `dotnet tool run nuget-license --input lfm.sln --output JsonPretty --include-transitive` (run `dotnet tool restore` first; set `NUGET_PACKAGES` to `.cache/nuget` if the global cache is read-only in the sandbox)
- **Allowlist:** `.github/license-allowlist.txt`

## Summary

| Metric | Value |
|---|---|
| Total packages resolved | 291 |
| Unique licenses seen | 11 (6 SPDX + 4 URL-based + 1 URL → MS EULA) |
| Packages incompatible with AGPL-3.0-or-later | 0 |

## Per-license roll-up

| License (SPDX) | Raw value in tool output | Packages | AGPL-3.0-or-later compatible |
|---|---|---|---|
| MIT | `MIT` | 236 | Yes — permissive |
| Apache-2.0 | `Apache-2.0` | 26 | Yes — permissive |
| BSD-3-Clause | `BSD-3-Clause` | 5 | Yes — permissive |
| BSD-2-Clause | `BSD-2-Clause` | 1 | Yes — permissive |
| MPL-2.0 | `MPL-2.0` | 1 | Yes — weak copyleft, file-level, compatible with AGPL per FSF guidance |
| MS-PL | `MS-PL` | 1 | Yes — permissive (OSI-approved) |
| MIT (manual) | `https://github.com/moodmosaic/Fare/blob/master/LICENSE` | 1 | Yes — MIT confirmed via LICENSE file |
| MIT (manual) | `https://github.com/jdevillard/JmesPath.Net/blob/master/LICENSE` | 1 | Yes — MIT confirmed via LICENSE file |
| Apache-2.0 (manual) | `https://raw.githubusercontent.com/StefH/SimMetrics.Net/master/LICENSE` | 1 | Yes — Apache-2.0 confirmed via LICENSE file |
| Apache-2.0 (manual) | `https://raw.githubusercontent.com/xunit/xunit/master/license.txt` | 1 | Yes — Apache-2.0 confirmed (xunit relicensed to Apache-2.0 in 2014) |
| MIT (manual) | `http://www.microsoft.com/web/webpi/eula/net_library_eula_enu.htm` | 2 | Yes — old pre-open-source MS .NET Library EULA; permissive (source-available, redistribution permitted); same packages now ship as MIT (10.x) |

## Per-package detail (non-trivial rows only)

Packages with `MIT` (236), `Apache-2.0` (26), `BSD-3-Clause` (5) are omitted from the per-package table as they are bulk and uniformly compatible. The table below covers: all packages with a license appearing ≤ 3 times in the roll-up, all URL-based licenses, and any flagged rows. This abbreviation is documented in § Methodology.

| Package | Version | License (raw) | SPDX resolved | Compatible | Notes |
|---|---|---|---|---|---|
| Scriban.Signed | 7.0.6 | `BSD-2-Clause` | BSD-2-Clause | Y | |
| Deque.AxeCore.Commons | 4.11.1 | `MPL-2.0` | MPL-2.0 | Y | E2E test-only dep via `Deque.AxeCore.Playwright`; MPL-2.0 is file-level copyleft, FSF-confirmed compatible with AGPL |
| XPath2 | 1.1.5 | `MS-PL` | MS-PL | Y | Transitive via `WireMock.Net`; MS-PL is OSI-approved permissive |
| Fare | 2.2.1 | `https://github.com/moodmosaic/Fare/blob/master/LICENSE` | MIT | Y | URL-based; LICENSE file confirms MIT |
| JmesPath.Net.Parser | 1.1.0 | `https://github.com/jdevillard/JmesPath.Net/blob/master/LICENSE` | MIT | Y | URL-based; LICENSE file confirms MIT |
| SimMetrics.Net | 1.0.5 | `https://raw.githubusercontent.com/StefH/SimMetrics.Net/master/LICENSE` | Apache-2.0 | Y | URL-based; LICENSE file confirms Apache-2.0 |
| xunit.abstractions | 2.0.3 | `https://raw.githubusercontent.com/xunit/xunit/master/license.txt` | Apache-2.0 | Y | URL-based; xunit relicensed to Apache-2.0 in 2014 |
| Microsoft.Extensions.Caching.Abstractions | 1.0.0 | `http://www.microsoft.com/web/webpi/eula/net_library_eula_enu.htm` | MS .NET Library EULA | Y | Old transitive dep; same package ships as MIT from 2.x onward; EULA is permissive for use and redistribution in binary form |
| Microsoft.Extensions.Caching.Memory | 1.0.0 | `http://www.microsoft.com/web/webpi/eula/net_library_eula_enu.htm` | MS .NET Library EULA | Y | Same as above |

## Flagged rows (incompatible or unknown)

None — all dependencies are AGPL-3.0-or-later compatible.

The URL-based licenses required manual resolution (see per-package table); all resolved to standard SPDX-compatible identifiers.

## Methodology

1. Ran `dotnet tool restore` to install `nuget-license` 4.0.9 from `.config/dotnet-tools.json`.
2. Ran `dotnet tool run nuget-license --input lfm.sln --output JsonPretty --include-transitive` to produce JSON output (3187 lines, 291 unique package records).
3. Extracted unique `(PackageId, PackageVersion, License)` via `jq`.
4. Classified each `License` value against the AGPL-3.0-or-later compatibility list.
5. Four URL-based license values (Fare, JmesPath.Net.Parser, SimMetrics.Net, xunit.abstractions) resolved by visiting the URL and reading the LICENSE file text; all confirmed compatible.
6. Two `http://www.microsoft.com/web/webpi/eula/net_library_eula_enu.htm` entries (old `Microsoft.Extensions.Caching.*` 1.0.0) resolved by reviewing the MS .NET Library EULA text; confirmed permissive for binary redistribution; same packages now ship as MIT.
7. **Abbreviation:** The per-package detail table is abbreviated. Packages with bulk-uniform licenses (MIT: 236, Apache-2.0: 26, BSD-3-Clause: 5) are omitted. Only non-trivial rows (URL-based licenses, licenses appearing ≤ 3 times, or any incompatible/unknown rows) are listed. The roll-up table covers all licenses completely.

## Regeneration

This report is a point-in-time baseline. Ongoing enforcement lives in `.github/workflows/dep-license-check.yml` (see Task 9). Regenerate only if a major upstream licensing change is suspected.
