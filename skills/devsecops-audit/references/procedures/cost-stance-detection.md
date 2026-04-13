# Cost Stance Detection

Resolve the audit's cost stance by walking the precedence chain. Return both the resolved stance (`free` | `mixed` | `full`) and the source (`arg` | `config.yaml` | `CLAUDE.md` | `default`) so the report footer can cite it.

## Inputs

- Invocation args (if any were passed to the skill or agent).
- `skills/devsecops-audit/config.yaml` (may or may not be present — if absent, this step contributes nothing).
- `CLAUDE.md` at the repo root.

## Precedence (highest wins)

1. `--cost-stance=<value>` in the invocation args
2. `skills/devsecops-audit/config.yaml` → `costStance` field
3. `CLAUDE.md` § "Cost Guidance" auto-detect
4. Hard default: `full`

## Step 1 — Invocation arg

Parse any token matching `--cost-stance=<value>` from the invocation args. Accept `free`, `mixed`, `full`. On invalid value, log a warning line (`invalid --cost-stance value: <value>, ignored`) and fall through to step 2.

If the arg is present and valid, return `{stance: <value>, source: arg}` and stop.

## Step 2 — Config file

Run:
```bash
test -f skills/devsecops-audit/config.yaml && yq eval '.costStance // "null"' skills/devsecops-audit/config.yaml
```

If the command prints `free`, `mixed`, or `full`, return `{stance: <that value>, source: config.yaml}` and stop. If it prints `null` or the file is missing, fall through to step 3.

When `costStance` is `mixed`, also run:
```bash
yq eval '.mixedEnabled[]' skills/devsecops-audit/config.yaml
```
to get the list of Band 2 codes to activate. Return these alongside the stance as `{stance: mixed, mixedEnabled: [...], source: config.yaml}`.

## Step 3 — `CLAUDE.md` auto-detect

Read `CLAUDE.md`. Locate any heading that matches (case-insensitive) `cost guidance`, `cost stance`, `cost ceiling`, or `hobby project`. In the body under that heading, search for any of:

- `prefer[s]?\s+free\s+tier`
- `free[- ]tier(\s+only)?`
- `hobby\s+project`
- `avoid\s+.*recurring\s+cost`

Use Grep, not raw shell:
```
Grep with pattern "prefer.*free.*tier|free[- ]tier|hobby.*project|avoid.*recurring.*cost" in CLAUDE.md (case-insensitive)
```

If any match, return `{stance: free, source: CLAUDE.md}` and stop. If no match, fall through to step 4.

## Step 4 — Hard default

Return `{stance: full, source: default}`.

A security skill defaulting to `full` is deliberate: silently suppressing Band 2 findings is a worse failure mode than firing too many. A repo that cares about cost must declare it.

## Output

The caller receives a struct equivalent to:
```
{stance: "free"|"mixed"|"full", source: "arg"|"config.yaml"|"CLAUDE.md"|"default", mixedEnabled: [code, ...]}
```

And emits a one-line disclosure for the report footer:
```
Cost stance: <stance> (source: <source>)
```

When stance is `mixed`, append:
```
Mixed Band 2 enabled: <comma-separated list of codes>
```

When stance is `free` (or `mixed` without a given extension's Band 2 codes), each extension that contributed Band 2 findings emits one `info` suppression line:
```
Cost stance: free → Band 2 smells suppressed for <extension>: <comma-separated codes>
```
