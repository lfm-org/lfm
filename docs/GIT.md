# Mandatory Git Workflow

1. Start every task with a clean workspace.
2. If workspace is not clean, stop and alert the user.
3. Work in a dedicated branch: `claude/<short-slug>`.
   - Default branch-creation command: `git switch -c claude/<short-slug>`.
   - Prefer `git switch -c` over `git checkout -b` unless a concrete repo-specific reason requires a different command form.
4. Keep changesets small by default:
   - Small commit target: `<= 5` files changed and `<= 250` total changed lines (additions + deletions).
   - Small task-branch target: `<= 30` files changed and `<= 900` total changed lines relative to `main`.
   - Thresholds are a planning and review aid, not a reason to degrade design quality or implementation clarity.
   - Do not force brittle shortcuts (for example embedding large build/config definitions inline solely to reduce file count).
   - Commit each coherent partial finish as soon as it is ready; do not defer finished implementation chunks to a single end-of-task commit.
   - Before branch closure recommendation, run a quick commit-stack review and normalize commit structure (split/squash/reword) when clarity or traceability needs improvement.
   - If projected work exceeds small-task thresholds, split automatically into sequenced subtasks/branches before continuing.
5. Merge strategy is rebase-and-merge.
6. Branch closure policy:
   - Claude may close task branches at their own consideration once closure prerequisites are satisfied.
   - No separate user close approval is required.
   - Branch closure must still use rebase-and-merge and keep workspace clean on `main`.
   - Closure prerequisites: all changes committed, no unstaged modifications, branch task complete.
   - After a skill (e.g. `/simplify`, `/security-review`) produces changes, commit them and close immediately if no further branch work remains — do not wait for the user to prompt closure.
7. Guidance changes based on user policy approvals must be documented in guidance files in the same task.
8. Close branch and return to clean `main` for the next task.
9. Commit message style: short, imperative subjects — e.g. `Fix docker`, `Add raids route`. Keep commits scoped and direct.
10. Pull request descriptions: explain the change, list any env or schema changes, and include screenshots for UI work.
