# Testing Guidelines

## The BDD Development Cycle (test-first)

Testing is test-first by default:

1. Write a failing Given/When/Then scenario before any implementation code.
2. Run the test — confirm it fails for the right reason (Red).
3. Write the minimum implementation to make it pass (Green).
4. Refactor freely — the passing test is your safety net (Refactor).

Never write implementation code without a failing test to justify it. The test drives the design.

## Naming Pattern

Use `Given / When / Then` in `describe` and `it` strings:

```typescript
describe("given a raid exists", () => {
  describe("when GET /api/raids is called without a cookie", () => {
    it("then it returns 401", async () => { ... });
  });
});
```

## Principles

- Test from the outside in: HTTP route → service behavior → DB state.
- For React components, use Testing Library with user-centric queries (`getByRole`, `getByText`) — never query by class name or internal state.
- One assertion per scenario where possible; name the scenario in the `it` string.
- Do **not** test implementation details (which Prisma method was called, internal cache state). Test what the caller observes.

## Test File Layout

- Route handler tests: `app/api/**/*.test.ts` — use `NextRequest` mocks
- Component tests: `components/**/*.test.tsx` — use `@testing-library/react`
- Utility tests: `util/**/*.test.ts`

## When to Write Tests

Tests run via Jest. Write a BDD scenario for every new public function, every new route handler, and every UI flow involving routing, login, or raid calendar logic. No coverage threshold is enforced, but test-first is the default stance.
