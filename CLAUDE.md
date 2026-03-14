# Repository Guidelines

All application code lives under `frontend/`. See [frontend/CLAUDE.md](frontend/CLAUDE.md) for frontend-specific guidance (commands, architecture, testing, gotchas).

Root `docker-compose.yml` has 2 services: `frontend` (Next.js) + `database` (PostgreSQL).

## Mandatory Git Workflow

1. Start every task with a clean workspace; stop and alert the user if not clean.
2. Work in a dedicated branch: `claude/<short-slug>` — use `git switch -c claude/<short-slug>`. Prefer `git switch -c` over `git checkout -b`. Always use `git -C` with absolute paths.
3. Keep changesets small: commits `<= 5` files / `<= 250` lines; branches `<= 30` files / `<= 900` lines vs `main`. Thresholds guide planning, not design — don't degrade quality to hit them. Commit partial finishes as you go; split into subtasks if a branch will exceed these.
4. Merge strategy is rebase-and-merge. Use `superpowers:finishing-a-development-branch` to close a branch; keep workspace clean on `main` after close.
5. Before claiming work complete, use `superpowers:verification-before-completion`. For non-trivial tasks, use `superpowers:requesting-code-review` before merging.
6. Commit message style: short, imperative subjects — e.g. `Fix docker`, `Add raids route`.
7. PR descriptions: explain the change, list env/schema changes, include screenshots for UI work.
8. Do not add `Co-Authored-By` trailers. AI usage is acknowledged in `README.md`.
9. Document any guidance changes in the same task's guidance files.

## Configuration & Secrets

Do not commit populated `.env` files or real Blizzard or database credentials. Use the checked-in `example.env` files as templates; keep local overrides out of version control.

**Required environment variables** (see `example.env` and `frontend/example.env` for full list):
- `DATABASE_URL` — PostgreSQL connection string (frontend only)
- `SISU_RAIDCAL_CLIENT_ID` / `SISU_RAIDCAL_CLIENT_SECRET` — Blizzard OAuth app credentials
- `BATTLE_NET_REGION` — e.g. `eu`
- `BATTLE_NET_REDIRECT_URI` / `APP_BASE_URL` — public URLs (no trailing slash; never hardcode localhost in production paths)
- `BATTLE_NET_COOKIE_SECURE` — `true` in production, `false` in local dev
- `HMAC_SECRET` — 64 random hex chars; generate with `openssl rand -hex 32`

## Documentation Separation

`CLAUDE.md` and `docs/` are Claude-facing: guidance, workflow rules, and architectural decisions. Do not mix in user-facing content — that belongs in `README.md` or `docs/user/`.

Plans and specs live in `docs/superpowers/plans/` and `docs/superpowers/specs/` respectively.
