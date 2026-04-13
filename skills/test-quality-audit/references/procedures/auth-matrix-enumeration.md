# Auth matrix enumeration

**When this runs:** step 2.6 of the deep-mode workflow in [../../SKILL.md](../../SKILL.md). Enumerates protected endpoints in the SUT and checks whether each cell of the auth scenario matrix has test coverage. Runs only in deep mode. Integration-rubric scope: skipped when the loaded extension has no auth-matrix section.

## Why a matrix instead of per-test smells

Core smells `I-HC-B7` and `E-HC-S3` already flag tests that only exercise the happy auth path. Those are per-test signals — they fire when a test is *there and incomplete*. Step 2.6 is a per-endpoint signal — it fires when a test is *missing* for an auth scenario. A happy-path-only test can trigger both: `I-HC-B7` as a per-test smell and `Gap-AuthZ` as a matrix gap.

## Matrix columns

At minimum, every protected endpoint should have a test for each of:

- **`anonymous`** — request with no credentials. Expected response typically `401 Unauthorized`.
- **`token-expired`** — request with a valid-format but expired token. Expected response typically `401 Unauthorized`.
- **`token-tampered`** — request with a valid-format token whose signature does not verify. Expected response typically `401 Unauthorized`.
- **`insufficient-scope`** — request with a valid token that lacks the required scope or role. Expected response typically `403 Forbidden`.
- **`sufficient-scope`** — request with a valid token that has the required scope. Expected response typically `200 OK` (or the endpoint's documented success code).
- **`cross-user`** — a user A request against a resource owned by user B. Expected response depends on the resource's ownership model: `403 Forbidden` or `404 Not Found`.

Extensions may add columns (e.g. `admin-only` for endpoints behind an admin policy).

## Procedure

1. **Read the extension's auth-matrix section.** If absent, record the skip and continue.
2. **Enumerate protected endpoints** by running the extension's auth-attribute patterns against the SUT. Collect route + HTTP method + required scope / role.
3. **For each endpoint × matrix column**, cross-reference against the test project. A cell is covered when:
   - A test body references the endpoint (via route template or Functions name) and
   - The test arranges the matching auth state (no token / expired token / tampered token / valid token with insufficient scope / valid token with sufficient scope / different-user token) and
   - The test asserts the documented response for that scenario.
4. **Emit a `Gap-AuthZ` entry** in the gap report for each endpoint with at least one uncovered cell. Confidence: high — auth matrix cells require explicit test setup; indirect coverage is rare.

## Output (appended to the step-5 gap report)

```markdown
#### Auth matrix coverage

| Endpoint | anonymous | expired | tampered | insufficient | sufficient | cross-user |
|---|---|---|---|---|---|---|
| `GET /api/users/me` | ✓ | ✗ | ✗ | ✗ | ✓ | n/a |
| `DELETE /api/orders/{id}` | ✓ | ✗ | ✗ | ✗ | ✓ | ✗ |

- **`Gap-AuthZ`**: `GET /api/users/me` — missing: token-expired, token-tampered, insufficient-scope. The endpoint exercises only happy + anonymous paths.
- **`Gap-AuthZ`**: `DELETE /api/orders/{id}` — missing: token-expired, token-tampered, insufficient-scope, cross-user. A cross-user test is especially important for a resource-owner endpoint.
```
