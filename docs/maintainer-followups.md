# Maintainer Followups — Public-Readiness Plan

Items **not addressed** by the public-readiness plan. These are
maintainer-owned external-surface concerns that live outside the
repository.

## External-surface items

| Item | Status | Owner | Notes |
|---|---|---|---|
| Domain registrar WHOIS privacy (`lfm-api.dinosauruskeksi.com`, `lfm.dinosauruskeksi.com`) | TBD | maintainer | Enable WHOIS privacy if the registrar supports it. |
| Battle.net developer account name on the OAuth client | TBD | maintainer | The account name is visible to end users during OAuth consent. Rename if desired. |
| Existing live Azure deployment (`lfm` RG) | Shared with public repo | maintainer | Option 1 chosen in Phase 5: `lfm-org/lfm` and `tommimarkus/lfm` deploy to the same Azure resources. |
| DNS records pointing at the current live deployment | Unchanged | maintainer | `lfm-api.dinosauruskeksi.com` + `lfm.dinosauruskeksi.com` continue pointing at the shared infrastructure. |
| `SWA_DEPLOYMENT_TOKEN` rotation cadence | Quarterly per SECURITY.md | maintainer | Rotated on 2026-04-17 during publication. Next rotation due 2026-07-17. |
| Stale Azure AD federated credential `github-main` | Pending cleanup | maintainer | Subject references `tommimarkus/sisu-raidcal` (pre-rename); this is why `Deploy` on `tommimarkus/lfm` has been failing. Either update subject to `tommimarkus/lfm` or delete. |
| Authorized SSH signing key on `tommimarkus` GitHub account | TBD | maintainer | Verify the signing key used for the `lfm-org/lfm` initial commit appears as "Verified" in GitHub UI; if not, add it under Settings → SSH and GPG keys → Signing Key. |

## Skipped plan steps

- **V6.3 — Scratch-RG deploy verification.** Skipped because the real production deploy of `lfm-org/lfm` succeeded end-to-end against the existing `lfm` RG, which is a stronger signal of deployability than a scratch-RG run. Fresh-deployer viability for a different Azure subscription remains formally untested but is supported by the Phase 4 README walkthrough.

## Publication-era decisions

- **New public repo:** `lfm-org/lfm` — created 2026-04-17.
- **Private backup repo:** `github.com/tommimarkus/lfm` — kept untouched.
- **First public commit SHA:** `0ea07bac773c128b86f38b2cf29c12dc1a3ef608`.
- **Initial Deploy run:** https://github.com/lfm-org/lfm/actions/runs/24554388580 — completed 2026-04-17T08:06:26Z.
- **Azure AD app:** `lfm-github-actions` (appId `e51070d2-8f8e-4633-bd59-99bb225d108c`). Federated credentials added for `lfm-public-main` (`repo:lfm-org/lfm:ref:refs/heads/main`) and `lfm-public-pr` (`repo:lfm-org/lfm:pull_request`).
- **Repo variables set:** all 18 (Azure identity, resource names, hostnames, security.txt vars, `SECURITY_POLICY_URL=https://github.com/lfm-org/lfm/blob/main/SECURITY.md`).
- **Repo secret set:** `SWA_DEPLOYMENT_TOKEN` rotated and stored.
