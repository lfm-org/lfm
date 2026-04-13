# Mutation testing (conditional)

**When this runs:** step 4 of the deep-mode workflow in [../../SKILL.md](../../SKILL.md), whenever the loaded extension's declared mutation tool is installed. Skipped gracefully otherwise. Never run in quick mode. Never run against E2E audit targets.

## Why it is part of deep mode

Static audit grades **test quality**. Mutation testing grades **test effectiveness**. The two catch different things:

- Static audit catches characterization tests (locked current behavior), mock-verification smells, and pasted-literal expected values. It cannot see whether assertions actually verify anything, nor whether files exist without tests at all.
- Mutation testing catches surviving mutants (code that changed without any test failing) and no-coverage gaps (code no test ever exercises). It cannot tell you whether a high-scoring test is pinning the right behavior or the wrong one.

Running both on the same audit produces a reconciliation: **agreement** where both say "fine" or both say "broken" is trustworthy; **disagreement** is where the real findings live. A file rated `strong` by static audit that has surviving mutants is the single highest-signal result mutation testing can produce.

## Procedure

1. **Read the extension's Mutation tool section.** Every extension that supports mutation testing must provide: detection command, run command, install instructions, and a known-SUT-limitations list. Extensions without this section have no mutation tool declared — skip step 4 entirely and note "no mutation tool declared for this stack" in the output.

   **E2E rubric targets are excluded from mutation testing.** The SUT for an E2E test is the whole deployed (or locally launched) stack driven through a real browser — there is no single compile unit a source-level mutator can operate on, and even if one existed, the mutation-kill signal would be dominated by browser timing rather than test assertions. If the audit target contains only E2E tests, report state B or state C as appropriate (no applicable tool) and move on. If the target mixes rubrics, run mutation testing against the unit and integration SUTs only and note the E2E exclusion explicitly in the output.
2. **Detect tool availability** by running the extension's detection command. It should be cheap and side-effect-free (e.g. a tool list query). Parse the result to decide whether the tool is installed.
3. **Check SUT shape against the extension's known-limitations list.** If the SUT matches a documented unsupported shape (e.g. Blazor WASM for Stryker.NET), skip the run with that reason and the suggested workaround.
4. **Run the tool** per the extension's run command. Use the extension's default reporters (typically `json` + `cleartext` + `html`).
5. **On run failure**, capture the failure reason from the tool's output. If it matches a documented limitation, treat as "skipped — limitation"; otherwise, treat as "attempted, failed — <reason>". In both cases, continue with static findings.
6. **On success**, parse the report and extract:
   - Overall mutation score.
   - Per-file killed / survived / no-coverage counts.
   - Surviving-mutant details for files the static audit rated `strong` (the audit-vs-mutation disagreements).
   - List of files with zero coverage (all mutants `NoCoverage`).

## Output states

In the step-5 suite assessment, the `Mutation testing` subsection must be present in exactly one of these three states:

**State A — ran successfully:**

```markdown
### Mutation testing

- **Tool:** <name and version>
- **Scope:** <which projects ran>
- **Mutation score:** <percentage> (<killed> killed / <survived> survived / <no_coverage> no-coverage / <timeout> timeout)
- **Files with zero coverage:** <count>, top examples: <file list>
- **Audit-vs-mutation agreements (static rated weak/adequate + survivors):** <file list>
- **Audit-vs-mutation disagreements (static rated strong + survivors):** <file list with counts> — highest-value findings
- **Notes:** <any scope caveats, e.g. "Blazor WASM SUT excluded per extension limitation">
```

**State B — tool not installed:**

```markdown
### Mutation testing

- **Status:** skipped — <tool name> not installed
- **To enable:** <install command from extension>
- **Why this matters:** <one sentence on what mutation testing adds>
```

**State C — attempted, failed (or limitation hit):**

```markdown
### Mutation testing

- **Status:** attempted, failed — <reason>
- **Tool:** <name and version>
- **Root cause:** <short explanation, referencing the extension's known-limitations section if applicable>
- **Workaround:** <from the extension, if one is documented>
- **Fallback:** static audit findings stand on their own.
```
