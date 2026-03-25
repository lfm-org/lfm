---
name: code-quality-review
description: Use when assessing the quality of an entire codebase, identifying systemic engineering risks, or producing a prioritized remediation plan for a repository-wide audit.
---

# Code Quality Review

## Overview

Assess repository-wide code quality with a metric-driven audit and produce a prioritized remediation plan.
Optimize for agentic development: prefer code that is easy to understand, modify in small units, and verify cheaply.

Read `references/browserstack-metrics.md` before scoring. It contains the approved metric order, tier weights, and assessment rules derived from the BrowserStack article and adapted for agentic workflows.

## Workflow

### 1. Establish review scope

- Confirm the request is for a whole codebase, not a pull-request diff review.
- Identify the main languages, frameworks, packages, services, and test or lint entry points.
- Note any missing tooling or setup problems before making quality claims.

### 2. Gather evidence

Use concrete repository signals before making judgments.

- Read top-level docs, package manifests, build scripts, and test configuration.
- Inspect representative modules, not just one hotspot.
- Run available validation commands when practical.
- Record blockers explicitly when tooling is missing or verification cannot run.

Look for evidence in these areas:

- architecture and module boundaries
- naming, local clarity, and control-flow simplicity
- test structure, determinism, and isolation
- performance-sensitive paths and obvious inefficiencies
- complexity, coupling, duplication, and oversized files
- documentation for setup, architecture, and extension points
- security-sensitive code paths, secrets handling, auth, and validation
- maintenance signals such as churn hotspots, debt markers, TODO clusters, and fragile workarounds

### 3. Score with the weighted metric model

Use the metric order and tiering from `references/browserstack-metrics.md`.

- Tier 1 metrics carry the most weight and determine whether the codebase is safe for repeated agentic edits.
- Tier 2 metrics strengthen or weaken the assessment but should not override severe Tier 1 weakness.
- Tier 3 metrics are supporting signals and must not dominate the conclusion.

Apply these weights:

- Tier 1: `5x`
- Tier 2: `3x`
- Tier 3: `1x`

Apply this gate:

- Do not rate the overall codebase as `good` if two or more assessed Tier 1 metrics are weak.

Prefer qualitative ratings backed by evidence:

- `strong`
- `adequate`
- `weak`
- `not assessed`

Use `not assessed` when the repository or environment does not provide enough trustworthy evidence to score a metric.
Do not invent numeric precision or certainty when the repository does not expose trustworthy measurements.

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

## Output Format

Use this structure unless the user asks for something else:

### Summary

- overall assessment
- major strengths
- major risks
- verification limits

### Metric Assessment

List all fifteen metrics in the approved order with:

- rating
- short evidence note

If a metric cannot be scored credibly, mark it `not assessed` and say what evidence is missing.

### Top Findings

List the highest-impact issues first.

### Prioritized Remediation Plan

List actionable work items in priority order.

### Residual Unknowns

Call out missing evidence, skipped commands, or areas that need deeper inspection.

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
