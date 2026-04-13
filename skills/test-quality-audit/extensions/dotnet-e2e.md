# Extension: .NET — E2E-rubric addon

Addon to [dotnet-core.md](dotnet-core.md) loaded **only when step 0b selects the E2E rubric**. Currently a stub — no framework-specific `dotnet.E-*` smells are declared yet. Add them here as patterns emerge from real audits.

**Prerequisite:** `dotnet-core.md` must already be loaded. The test-type detection signals that route a test to the E2E rubric (Playwright / Selenium imports, browser session types, `PageTest` / `ContextTest` base classes) live in `dotnet-core.md` because dispatch happens before rubric selection.

**Note on procedures not applicable to E2E:** `dotnet-core.md` declares the Stryker mutation tool, SUT surface enumeration, and determinism verification. Per [SKILL.md § Mutation testing (conditional)](../SKILL.md#mutation-testing-conditional) and [SKILL.md § Determinism verification](../SKILL.md#determinism-verification), those procedures **do not apply to E2E audit targets** — the SUT for an E2E test is the whole deployed stack driven through a browser, and neither mutation testing nor determinism rerun is meaningful against that target. The audit agent must skip those steps when the selected rubric is E2E, regardless of whether `dotnet-core.md` declared the tools.

---

## Sub-lane classification hints

Core [SKILL.md § 0b step 5](../SKILL.md#0b-select-the-rubric) already lists the canonical sub-lane routing signals (`[Trait("Category", "Accessibility")]`, axe / `AxeBuilder`, Web Vitals / `PerformanceObserver`, CSP / cookie assertions). This file may add .NET-specific refinements for Playwright and Selenium idioms as real audit cases expose them. None declared yet.

---

## Framework-specific high-confidence E2E smells (`dotnet.E-HC-*`)

*(No entries yet. Add `dotnet.E-HC-F*`, `dotnet.E-HC-A*`, `dotnet.E-HC-P*`, `dotnet.E-HC-S*` here as Playwright / Selenium-specific failure patterns emerge. Each entry follows the standard extension-smell shape: `Applies to:` line, detection hint, smell description, example, and rewrite.)*

---

## Framework-specific low-confidence E2E smells (`dotnet.E-LC-*`)

*(No entries yet.)*

---

## Framework-specific E2E positive signals (`dotnet.E-POS-*`)

*(No entries yet. Candidates worth rewarding once an example is found: Playwright's `GetByRole` / `GetByLabel` accessible-name locators, per-test `BrowserContext` factories, `Expect(locator).ToHaveTextAsync` condition-based waits over `Thread.Sleep`.)*
