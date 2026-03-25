# BrowserStack Metrics Adapted For Agentic Development

This reference reorders BrowserStack's 15 code quality metrics for whole-codebase reviews where the primary goal is safe, repeatable agentic development.

Source article:
- https://www.browserstack.com/guide/software-code-quality-metrics

## Priority Order

1. `Maintainability`
2. `Testability`
3. `Reliability`
4. `Efficiency`
5. `Readability`
6. `Documentation`
7. `Cyclomatic Complexity`
8. `Technical Debt`
9. `Extensibility`
10. `Code Security`
11. `Unit Test Results`
12. `Code Churn`
13. `Code Coverage`
14. `Reusability`
15. `Portability`

## Tier Model

### Tier 1: primary gates

- `Maintainability`
- `Testability`
- `Reliability`
- `Efficiency`
- `Readability`

Why:
- These most directly control whether an agent can understand code, change it in a bounded area, and verify the result with acceptable risk.

### Tier 2: strong secondary signals

- `Documentation`
- `Cyclomatic Complexity`
- `Technical Debt`
- `Extensibility`
- `Code Security`

Why:
- These materially affect long-term change safety and review confidence, but usually act through the top-tier metrics rather than replacing them.

### Tier 3: context-dependent supporting signals

- `Unit Test Results`
- `Code Churn`
- `Code Coverage`
- `Reusability`
- `Portability`

Why:
- These can be useful leading indicators, but they are easy to misread without surrounding context.

## Weighting

- Tier 1: `5x`
- Tier 2: `3x`
- Tier 3: `1x`

Gate:
- The overall codebase assessment cannot be `good` if two or more Tier 1 metrics are weak.

## What To Look For

### Maintainability

- clear module boundaries
- focused files and functions
- low coupling
- easy local modification without broad regressions

### Testability

- deterministic tests
- isolated side effects
- seams around IO, time, randomness, and external services
- clear test entry points

### Reliability

- stable behavior under normal and edge conditions
- error handling
- recovery behavior
- evidence of regression protection

### Efficiency

- avoidable hot-path waste
- unnecessary repeated work
- pathological rendering or query patterns
- performance issues that raise the cost of verification or change

### Readability

- descriptive naming
- linear control flow
- low nesting
- intent that is obvious without reverse engineering

### Documentation

- setup instructions that work
- architecture notes
- ownership or subsystem boundaries
- extension guidance for common changes

### Cyclomatic Complexity

- deeply branched logic
- large conditional trees
- functions that require too much state-tracking to reason about safely

### Technical Debt

- TODO and FIXME clusters
- workaround layering
- stale abstractions
- known compromises with no cleanup path

### Extensibility

- clear extension points
- pluggable interfaces
- ability to add behavior without invasive edits

### Code Security

- auth and authorization boundaries
- input validation
- secret handling
- unsafe deserialization or injection risks

### Unit Test Results

- whether tests pass
- whether failures are concentrated in quality hotspots
- whether passing tests appear trustworthy or brittle

### Code Churn

- frequently edited areas
- hotspots that also show complexity, reliability, or efficiency issues

### Code Coverage

- coverage used as a supporting signal only
- meaningful assertions over raw percentages

### Reusability

- shared components that reduce duplication without creating harmful coupling

### Portability

- cross-platform correctness
- environment independence
- deployability across expected targets

## Interpretation Rules

- Prefer trends and interacting signals over isolated numbers.
- Treat coverage, churn, and unit pass rate as supporting evidence.
- Favor findings that explain why the code is or is not safe for repeated machine-assisted edits.
- Distinguish direct observation from inference when metrics are estimated from source inspection rather than tooling.
