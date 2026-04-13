# Extensions

Per-stack smell packs loaded on demand by the devsecops-audit skill. The core workflow in `../SKILL.md` is framework-neutral; extensions add stack-specific grep patterns, smell codes, positive signals, and carve-outs.

## Load order

1. The skill detects target type(s) by globbing the audit target.
2. Every matching extension is loaded.
3. Each extension's smells, positive signals, and carve-outs are added to the active rubric.
4. On conflict between a carve-out and a core smell, the carve-out wins — but only for the exact pattern described.

Extensions **never override** core rules. They may only **add** or **carve out**.

## File layout per extension

Each extension is a single markdown file in this directory. Required sections:

- **Name and detection signals** — which files trigger the load (globs + content matches).
- **Smell codes** — namespaced as `<ext>.HC-N`, `<ext>.LC-N`, `<ext>.POS-N`. Band 2 codes use `<ext>.B2-N`.
- **Smell table** — one row per code with: grep pattern, severity default, rubric back-reference, remediation action template.
- **Carve-outs** — explicit `do not flag <core code> when <pattern>` entries.
- **Applies to** — which rubric sections this extension addresses.

## Current extensions

| File | Applies to | Notes |
|---|---|---|
| `github-actions.md` | `.github/workflows/*.yml` | CI workflow smells; maps into CICD-SEC-1/2/4/5/6 |
| `bicep.md` | `infra/**/*.bicep` | Azure IaC; Band 1 always on, Band 2 cost-gated |
| `dockerfile.md` | `**/Dockerfile`, `**/docker-compose*.yml` | Container image and compose smells |
| `dotnet-security.md` | C# under `api/` `app/` `shared/` `tests/` | Code-level security smells in .NET |

## Cost banding

Extensions that cover Azure resources (currently `bicep.md`) split their smell codes into two bands by remediation cost:

- **Band 1 — always-block.** Remediation is free or near-free (config toggle, IaC edit). Fires under all cost stances.
- **Band 2 — cost-gated.** Remediation requires a paid Azure SKU. Fires only under `costStance: full` or when explicitly listed in `mixedEnabled`.

See `../references/procedures/cost-stance-detection.md` for how the stance is resolved.

## Adding a new extension

1. Copy one of the existing extension files as a template.
2. Pick a short name prefix (e.g. `tf` for Terraform, `k8s` for Kubernetes).
3. Add detection signals.
4. Add smell codes, namespaced with your prefix.
5. Add the extension to the catalog at `../references/smell-catalog.md`.
6. Add the extension to the table above.
7. Update `../SKILL.md` § "Extensions" with the new detection mapping.
8. If the extension covers Azure (or any other cloud with paid security SKUs), split its codes into Band 1 / Band 2 / positive.
