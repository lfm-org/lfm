# Deep mode output format

**When this runs:** read before producing the deep-mode output in steps 5 and 6 of the workflow in [../../SKILL.md](../../SKILL.md). Defines the templates for the per-file rollup, the suite-level assessment (pyramid ratio, gap report, runtime distribution, determinism findings, mutation testing subsection), and the prioritized remediation worklist. Quick-mode audits only emit per-test findings and do not use these templates.

## Per-file rollup

After all per-test findings, emit a per-file rollup table:

```markdown
## Per-file rollup

| File | Tests | Spec | Char | Ambig | Top smells | Grade |
|---|---|---|---|---|---|---|
| `OrderServiceTests.cs` | 14 | 6 | 5 | 3 | HC-5, HC-7, dotnet.HC-1 | weak |
```

Columns:

- **Tests** — total per file.
- **Spec / Char / Ambig** — verdict counts. For integration-rubric files, substitute `Incidental` for `Char`; for E2E, include the dominant verdict per sub-lane.
- **Top smells** — the 2–4 most frequent codes by count.
- **Grade** — `strong` / `adequate` / `weak` / `not assessed`.

## Suite assessment

Then emit the suite-level assessment block:

```markdown
## Suite assessment

- **Extensions loaded:** dotnet
- **Overall verdict:** <strong / adequate / weak / not assessed>
- **Top risks:** <3-5 bullets by impact>
- **Verification limits:** <what neither static audit nor mutation testing can determine>

### Pyramid ratio

- **Unit:** <N tests> (<percentage>)
- **Integration:** <N tests> (<percentage>)
- **E2E:** <N tests> (<percentage>) — break down by sub-lane F/A/P/S when non-zero
- **Shape:** `pyramid` / `diamond` / `inverted` / `hourglass`
- **Finding:** <none / "unit coverage is thin — <percentage> unit vs Google 70-80% guidance" / "E2E inflated — <percentage> vs ≤10% guidance">

### Gap report

<One of the states documented in [sut-surface-enumeration.md § Gap report format](sut-surface-enumeration.md#gap-report-format):
 State A — enumeration ran (SUT projects list, counts table, top probable gaps, reconciliation with mutation)
 State B — skipped (extension has no SUT surface enumeration section / quick mode / E2E-only scope)>

### Runtime distribution

- **Source:** `.trx` / JUnit XML file parsed from the most recent test run (or "none found — skipped")
- **Top 10 slowest tests:** `<test name> — <elapsed>` (one line each)
- **Findings:** <any unit test > 100 ms; any in-process integration test > 2 s; total-time warning if > 5 min>

### Determinism findings

<One of:
 Section from [determinism-verification.md](determinism-verification.md) (runtime-proven flakes with suspected causes)
 Skipped (reason: suite too large / extension has no cheap-rerun command / no static smells to motivate run)>

### Mutation testing

<One of the three states documented in [mutation-testing.md § Output states](mutation-testing.md#output-states):
 State A — ran successfully (score + reconciliation bullets)
 State B — skipped, tool not installed (install command + one-liner rationale)
 State C — attempted and failed, or hit documented limitation (root cause + workaround)>

## Prioritized remediation worklist

- **P0** — <work item, expected impact, effort estimate>
- **P1** — <work item> `[mutation]` ← tag items surfaced by mutation testing
- **P2** — ...
```
