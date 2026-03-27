# BrowserStack Metrics Adapted For Agentic Development

This reference reorders BrowserStack's 15 code quality metrics for whole-codebase reviews where the primary goal is safe, repeatable agentic development.

Source article:
- https://www.browserstack.com/guide/software-code-quality-metrics

## Priority Order

1. `Maintainability`
2. `Testability`
3. `Reliability`
4. `Readability`
5. `Documentation`
6. `Efficiency`
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
- `Readability`
- `Documentation`

Why:
- These most directly control whether an agent can understand code, orient itself in the codebase, change code in a bounded area, and verify the result with acceptable risk.
- Documentation is Tier 1 because agents cannot intuit context from team experience. They depend on written architecture notes, setup guides, and extension points to navigate unfamiliar code.

Gate:
- The overall codebase assessment cannot be `good` if two or more Tier 1 metrics are weak.

### Tier 2: strong secondary signals

- `Efficiency`
- `Cyclomatic Complexity`
- `Technical Debt`
- `Extensibility`
- `Code Security`

Why:
- These materially affect long-term change safety and review confidence, but usually act through the top-tier metrics rather than replacing them.
- Efficiency matters for users and can raise the cost of verification, but does not typically block an agent from making correct changes.

### Tier 3: context-dependent supporting signals

- `Unit Test Results`
- `Code Churn`
- `Code Coverage`
- `Reusability`
- `Portability`

Why:
- These can be useful leading indicators, but they are easy to misread without surrounding context.

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

### Efficiency

- avoidable hot-path waste
- unnecessary repeated work
- pathological rendering or query patterns
- performance issues that raise the cost of verification or change

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

## Supplementary Agentic Signals

These are not standalone metrics. Look for these signals when assessing the metrics above, and call them out in findings when they materially affect agentic development safety.

### Type Safety

Assess alongside Reliability and Maintainability.

- presence and strictness of static type checking
- type coverage across the codebase
- whether types provide meaningful compile-time feedback for common changes
- typed APIs that prevent hallucinated method calls or wrong argument types

### Change Isolation

Assess alongside Maintainability and Testability.

- blast radius of typical edits — how many files does a small change touch?
- module boundaries that contain ripple effects
- import graphs that show concentrated vs. diffuse dependencies

### Feedback Loop Speed

Assess alongside Testability and Efficiency.

- time from code change to build/type-check/lint/test feedback
- whether fast verification paths exist for incremental changes
- test suite runtime and whether focused test runs are supported

## Interpretation Rules

- Prefer trends and interacting signals over isolated numbers.
- Treat coverage, churn, and unit pass rate as supporting evidence.
- Favor findings that explain why the code is or is not safe for repeated machine-assisted edits.
- Distinguish direct observation from inference when metrics are estimated from source inspection rather than tooling.
