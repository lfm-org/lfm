# Migration upgrade-path enumeration

**When this runs:** step 2.7 of the deep-mode workflow in [../../SKILL.md](../../SKILL.md). For each database migration in the SUT, checks that there is an upgrade-path test that runs the migration against a representative prior-schema state (not an empty database). Runs only in deep mode. Integration-rubric scope: skipped when the loaded extension has no migration-upgrade section.

## Why an upgrade-path test, not a smoke test

`I-HC-A7` flags migration tests run against an empty database as a smell — an empty-DB test passes for any migration that doesn't throw, including a broken one. The corollary is: every migration needs a test with seed data that represents a plausible N-1 state, applying the migration, and asserting that (a) existing rows still query correctly, (b) new columns have the expected default, and (c) the migration is idempotent if re-run.

For a repo with an expand-only migration rule (see `CLAUDE.md` — "Migrations must be additive (expand-only)"), upgrade-path testing is the mechanism that enforces the rule: a migration that breaks existing queries fails its upgrade-path test.

## Procedure

1. **Read the extension's migration-upgrade section.** If absent, record the skip and continue.
2. **Enumerate migration classes** via the extension's migration pattern (for .NET: files under `api/Migrations/*.cs`, or classes inheriting from a migration base type).
3. **For each migration**, check whether at least one test method:
   - References the migration class name (via `new MigrationX()` or `typeof(MigrationX)`).
   - Arranges non-empty seed data before invoking the migration (via `CreateItemAsync` / `InsertAsync` / similar on the underlying data store) — specifically, the test body contains at least one insertion call before the migration invocation.
   - Asserts post-migration state by querying the store or by reading at least one post-migrated row and asserting its shape.
4. **Emit a `Gap-MigUpgrade` entry** in the gap report for each migration that lacks an upgrade-path test. Confidence: high — migration upgrade tests are mechanical and their absence is unambiguous.

## Output (appended to the step-5 gap report)

```markdown
#### Migration upgrade-path coverage

- **Enumerated:** <N migration classes>
- **With upgrade-path test:** <M>
- **Probable gaps:** <N-M>

- **`Gap-MigUpgrade`**: `AddOrderStatusColumnMigration` (`api/Migrations/0007_order_status.cs`) — no test arranges non-empty seed data before running the migration. An empty-DB test (`I-HC-A7`) does not count.
- **`Gap-MigUpgrade`**: `RenameCustomerEmailFieldMigration` (`api/Migrations/0008_rename_email.cs`) — no upgrade-path test. This migration renames a field; absence of an N-1-state test means breakage would only surface in production.
```
