---
name: code-quality-review
description: Use when assessing the quality of an entire codebase, identifying systemic engineering risks, or producing a prioritized remediation plan for a repository-wide audit. Supports both light checks and comprehensive deep analysis.
---

# Code Quality Review

## Overview

Assess repository-wide code quality and produce findings scaled to the requested depth.
Optimize for agentic development: prefer code that is easy to understand, modify in small units, and verify cheaply.

Read `references/browserstack-metrics.md` before scoring. It contains the approved metric order, tier model, gate rule, and assessment criteria adapted for agentic workflows.

## Review Mode

This skill supports two modes. Choose based on the request:

### Light mode

Use for: quick health pulse, pre-work orientation, spot-checking after changes.

- Assess **Tier 1 metrics only** (5 metrics)
- Evidence: repo-native tools (build, test, lint) plus manual inspection
- No external tooling required — skip the bootstrap script
- Output: summary, Tier 1 scores, top risks, confidence note

Trigger phrases: "quick check", "how's the code quality", "health check", "light review", or any request that does not ask for remediation or a full audit.

### Deep mode

Use for: formal audit, planning a quality improvement program, onboarding a codebase for sustained agentic work.

- Assess **all 15 metrics** across all three tiers
- Evidence: full tooling stack from `references/tooling-evidence.md` where available
- Produce a prioritized remediation plan
- Output: full metric assessment, findings, remediation roadmap, residual unknowns

Trigger phrases: "full audit", "comprehensive review", "remediation plan", "deep analysis", or any request that explicitly asks for a prioritized improvement plan.

**Default:** If the request is ambiguous, ask the user which mode they want.

---

## Light Mode Workflow

### 1. Identify the codebase

- Note the main languages, frameworks, and services.
- Identify repo-native validation commands (build, test, lint).
- If repo-native commands exist and are practical to run, run them.

### 2. Assess Tier 1 metrics

Read `references/browserstack-metrics.md` for the Tier 1 list and assessment criteria.

For each Tier 1 metric:

- Inspect representative code, not just one file.
- Use repo-native tool output as evidence where available.
- Rate as `strong`, `adequate`, `weak`, or `not assessed`.
- Write one sentence of evidence per metric.

Also note any supplementary agentic signals (type safety, change isolation, feedback loop speed) from `references/browserstack-metrics.md` that stand out during inspection.

### 3. Write the light report

#### Summary

- Overall Tier 1 assessment (apply the gate: cannot be `good` if 2+ Tier 1 metrics are weak)
- Top risks (up to 3)
- Confidence note (what evidence was available, what was missing)

#### Tier 1 Scores

List each Tier 1 metric with rating and one-sentence evidence.

#### Notable Agentic Signals

Only include if something stood out during inspection (type safety gaps, high blast radius, slow feedback loop). Omit this section if nothing notable was found.

---

## Deep Mode Workflow

### 1. Establish review scope

- Confirm the request is for a whole codebase, not a pull-request diff review.
- Identify the main languages, frameworks, packages, services, and test or lint entry points.
- Note any missing tooling or setup problems before making quality claims.

### 2. Gather evidence

Use concrete repository signals before making judgments.

Read `references/tooling-evidence.md` and follow its execution order and fallback path:

- If external tools are available (Docker Sandbox, local install), use the bootstrap planner and full toolkit.
- If external tools are not available, follow the no-tools fallback: rely on repo-native commands plus manual inspection, and mark direct-metric scores as `not assessed` or note reduced confidence.

Evidence gathering checklist:

- Read top-level docs, package manifests, build scripts, and test configuration.
- Inspect representative modules, not just one hotspot.
- Run available validation commands when practical.
- Record blockers explicitly when tooling is missing or verification cannot run.

Use the evidence classes from `references/tooling-evidence.md`:

- direct metrics should be backed by tool output
- derived metrics should combine tool output with repository inspection
- inspection-dominant metrics should stay review-driven even if tools provide supporting signals

Look for evidence in these areas:

- architecture and module boundaries
- naming, local clarity, and control-flow simplicity
- test structure, determinism, and isolation
- performance-sensitive paths and obvious inefficiencies
- complexity, coupling, duplication, and oversized files
- documentation for setup, architecture, and extension points
- security-sensitive code paths, secrets handling, auth, and validation
- maintenance signals such as churn hotspots, debt markers, TODO clusters, and fragile workarounds

### 3. Score with the tier model

Use the metric order and tiering from `references/browserstack-metrics.md`.

- Tier 1 metrics are the primary gates and determine whether the codebase is safe for repeated agentic edits.
- Tier 2 metrics strengthen or weaken the assessment but should not override severe Tier 1 weakness.
- Tier 3 metrics are supporting signals and must not dominate the conclusion.
- Tool output is evidence only. Do not invent numeric rollups.

Apply the gate:

- Do not rate the overall codebase as `good` if two or more assessed Tier 1 metrics are weak.

Prefer qualitative ratings backed by evidence:

- `strong`
- `adequate`
- `weak`
- `not assessed`

Use `not assessed` when the repository or environment does not provide enough trustworthy evidence to score a metric.
Do not invent numeric precision or certainty when the repository does not expose trustworthy measurements.

Also note any supplementary agentic signals (type safety, change isolation, feedback loop speed) that materially affect the assessment.

### 4. Write findings

Order findings by engineering impact, not by file path or discovery order.

Each finding should include:

- the metric or metrics affected
- the concrete evidence
- why it matters for agentic development
- the likely consequence if left alone

Prefer findings that connect multiple signals, for example:

- high complexity plus weak tests
- poor documentation plus high coupling
- efficiency issues in code that also changes frequently
- weak type safety plus high blast radius

### 5. Produce a remediation roadmap

Turn findings into a prioritized worklist.

For each work item, include:

- priority: `P0`, `P1`, `P2`, or `P3`
- target metric improvement
- expected impact
- estimated effort: `small`, `medium`, or `large`
- suggested sequencing dependencies

Prioritize work that improves multiple top-tier metrics at once, such as:

- splitting oversized modules
- isolating side effects
- adding deterministic tests around unstable boundaries
- removing performance bottlenecks from frequently touched paths
- documenting architecture and extension points for core subsystems

### Deep mode output format

Use this structure unless the user asks for something else:

#### Summary

- overall assessment
- major strengths
- major risks
- verification limits

#### Metric Assessment

List all fifteen metrics in the approved order with:

- rating
- short evidence note

If a metric cannot be scored credibly, mark it `not assessed` and say what evidence is missing.

#### Top Findings

List the highest-impact issues first.

#### Prioritized Remediation Plan

List actionable work items in priority order.

#### Residual Unknowns

Call out missing evidence, skipped commands, or areas that need deeper inspection.

---

## Review Rules

- Prefer repository-wide patterns over isolated code smells.
- Avoid vanity conclusions based on coverage or churn alone.
- Treat passing tests as partial evidence, not proof of quality.
- If tooling cannot run, say so plainly and downgrade confidence.
- Distinguish observed evidence from inference.
- Do not guess a rating when the evidence is missing; use `not assessed`.
- Keep the review read-only unless the user explicitly asks for fixes.

## Common Mistakes

- Ranking code coverage above testability or reliability.
- Calling a codebase maintainable because style is clean while module boundaries are poor.
- Treating documentation as optional when agents must navigate the code repeatedly.
- Ignoring efficiency until late, even when hot paths are obvious and materially affect safe iteration.
- Giving a positive overall rating despite multiple weak Tier 1 metrics.
- Running the full deep-mode workflow when the user only asked for a quick check.
- Inventing numeric scores or weighted rollups when the evidence is qualitative.
