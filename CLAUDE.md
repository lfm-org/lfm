# Repository Guidelines

All application code lives under `app/`. See [app/CLAUDE.md](app/CLAUDE.md) for app-specific guidance (commands, architecture, testing, gotchas).

Root `docker-compose.yml` has 2 services: `app` (Next.js) + `database` (PostgreSQL).

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

**Required environment variables** (see `example.env` and `app/example.env` for full list):
- `DATABASE_URL` — PostgreSQL connection string (app only)
- `SISU_RAIDCAL_CLIENT_ID` / `SISU_RAIDCAL_CLIENT_SECRET` — Blizzard OAuth app credentials
- `BATTLE_NET_REGION` — e.g. `eu`
- `BATTLE_NET_REDIRECT_URI` / `APP_BASE_URL` — public URLs (no trailing slash; never hardcode localhost in production paths)
- `BATTLE_NET_COOKIE_SECURE` — `true` in production, `false` in local dev
- `HMAC_SECRET` — 64 random hex chars; generate with `openssl rand -hex 32`

## Tool Configuration

When configuring any tool (linters, bundlers, test runners, package managers, etc.) prefer configuration methods in this order:

1. **Config file** — use the tool's dedicated config file (`eslint.config.js`, `tsconfig.json`, `jest.config.ts`, `.npmrc`, etc.) where supported. Config files are version-controlled, discoverable, and IDE-aware.
2. **CLI argument** — use flags when the option isn't supported in a config file, or for one-off overrides (e.g. `--cache-dir`).
3. **Environment variable** — use env vars only when config files and CLI arguments are unavailable for the option.

### Sandbox cache root

Claude Code runs in a sandboxed environment where writes to `~/` locations (e.g. `~/.npm`, `~/.cache`, `~/.config`) are blocked. Use `.cache/` (project-local, git-ignored) as the cache root for any tool that cannot use its default location:

| Tool | Config file approach (preferred) |
|------|----------------------------------|
| npm | `cache=.cache/npm` in `app/.npmrc` |
| Playwright | `PLAYWRIGHT_BROWSERS_PATH=.cache/ms-playwright` (env var, no config file support) |
| ESLint | `cacheLocation: ".cache/eslint"` in `eslint.config.js` |

`.cache/` is git-ignored (contents only; `.cache/.gitkeep` is tracked so the directory exists in fresh checkouts).

### JSON processing

Use `jq` for any JSON processing in shell, not Python. Python one-liners for JSON are verbose, often require multiple iterations to produce the expected output, and are harder to read at a glance. `jq` is purpose-built, composable, and produces correct results first try.

```bash
# Good
jq '.raids[] | select(.visibility == "PUBLIC") | .id' raids.json

# Avoid
python3 -c "import json,sys; data=json.load(open('raids.json')); print([r['id'] for r in data['raids'] if r['visibility']=='PUBLIC'])"
```

## Documentation Separation

`CLAUDE.md` and `docs/` are Claude-facing: guidance, workflow rules, and architectural decisions. Do not mix in user-facing content — that belongs in `README.md` or `docs/user/`.

Plans and specs live in `docs/superpowers/plans/` and `docs/superpowers/specs/` respectively.
