---
name: validate-hygiene
description: >-
  Validate codebase hygiene — find unused dependencies, dead exports, stale env
  vars, orphaned routes, unused scripts, dead Bicep params, and more. Use when
  running verification checks, before merging branches, after cleanup work, or
  when the user asks about dead code, unused files, or codebase health. Invoke
  with /validate-hygiene or /validate-hygiene --strict for a mode that reports
  failure when issues exist.
---

# Codebase Hygiene Validation

Run automated checks to find dead code, unused dependencies, stale references, and orphaned artifacts. Two tools do the work:

- **knip** — unused deps, exports, files, types (runs per-package)
- **scripts/validate-hygiene.mjs** — i18n keys, env vars, routes, Bicep params (project-wide)

## Arguments

- No args: run all checks, print report
- `--strict`: same report, but end with **FAIL** if any findings exist

## Steps

### 1. Run knip (frontend + functions)

knip configs live in this skill's `config/` directory. Run from each package directory with `--config` pointing to the skill-local config.

```bash
cd frontend && npx knip --config ../.claude/skills/validate-hygiene/config/knip-frontend.json 2>&1; echo "EXIT:$?"
```

```bash
cd functions && npx knip --config ../.claude/skills/validate-hygiene/config/knip-functions.json 2>&1; echo "EXIT:$?"
```

knip exit code 1 = findings exist, 0 = clean.

### 2. Run custom checks

```bash
node .claude/skills/validate-hygiene/scripts/validate-hygiene.mjs 2>&1; echo "EXIT:$?"
```

Outputs JSON with four sections: `i18n`, `envVars`, `routes`, `bicep`. Exit code 1 = findings, 0 = clean.

### 3. Format the report

Combine knip + custom script output into this format:

```
## Codebase Hygiene Report

### knip — frontend
[knip output or "Clean"]

### knip — functions
[knip output or "Clean"]

### Unused i18n keys
[findings or "Clean"]

### Stale environment variables
[findings or "Clean"]

### Unused routes
[findings or "Clean"]

### Dead Bicep parameters
[findings or "Clean"]

### Summary
- X sections clean
- Y sections with findings

[PASS or FAIL if --strict]
```

In `--strict` mode, end with `**FAIL**` if either knip or the script found anything, `**PASS**` if both are clean.

## Prerequisite

knip must be installed as a devDependency in both `frontend/` and `functions/`. If missing:

```bash
npm --prefix frontend install --save-dev knip
npm --prefix functions install --save-dev knip
```

The custom script has zero dependencies — it uses only Node.js builtins.
