# Security Guidelines

## Configuration & Secrets

Do not commit populated `.env` files or real Blizzard or database credentials. Use the checked-in `example.env` files as templates, and keep local overrides out of version control.

## Dependency Pinning

**Pin exact versions — never use ranges for dependencies.**
Using `^` or `~` silently accepts future versions that may introduce vulnerabilities or supply-chain compromises. Always specify exact versions:

```json
"next": "15.3.9"   ✓
"next": "^15.0.0"  ✗  accepts any future 15.x
```

When adding or upgrading a dependency:
1. Look up the exact latest version via `npm view <pkg> version` or the npm registry — do not guess version numbers.
2. Use context7 or the package changelog to confirm the version exists and is stable before writing it.
3. After updating `package.json`, regenerate the lock file (`npm install`) so `package-lock.json` pins the full transitive tree.
4. Commit `package-lock.json` alongside `package.json` — the lock file is the real supply-chain protection.

## OWASP Controls

- **A01 (Broken Access Control):** All user-facing redirects in the Battle.net auth flow are normalised to relative paths to prevent open redirect.
- **A02/A09 (Sensitive Data / Logging):** Never log or expose access tokens, Battle.net credentials, or database passwords.
- **A03 (Injection):** Prisma uses parameterised queries throughout — do not concatenate user input into raw queries.
- **A07 (Auth):** OAuth tokens are stored in HttpOnly cookies — not accessible to JavaScript.
