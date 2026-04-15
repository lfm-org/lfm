# DevSecOps Audit Follow-up — 2026-04-15

Items from the [2026-04-14 DevSecOps audit](2026-04-14-devsecops-audit.md) that require manual user action after the remediation plan has landed. The plan itself is at [`docs/superpowers/plans/2026-04-15-devsecops-audit-remediation.md`](../superpowers/plans/2026-04-15-devsecops-audit-remediation.md).

## User actions

- [ ] **Enable signed commits on this workstation.** Pick one:

  **SSH signing** (simpler if you already have an SSH key on GitHub):

  ```bash
  git config --global gpg.format ssh
  git config --global user.signingkey ~/.ssh/id_ed25519.pub
  git config --global commit.gpgsign true
  ```

  Then add the same key as a **signing** key at https://github.com/settings/keys — the auth and signing key roles are separate checkboxes.

  **GPG signing:** see https://docs.github.com/en/authentication/managing-commit-signature-verification.

  **Verify:** your next commit should show a "Verified" badge in the GitHub UI.

- [ ] **Push the accumulated commits to `origin/main`.** All five phases merged locally but nothing has been pushed yet. Run:

  ```bash
  git -C /home/souroldgeezer/repos/lfm status
  git -C /home/souroldgeezer/repos/lfm push origin main
  ```

  The push triggers the first run with the new workflow chain (`secrets-scan` + `analyze-infra` → `deploy-infra` → `deploy-app` → `release`). Monitor the run page for any issues.

- [ ] **Verify the first release artifact after push.** After the first post-push deploy succeeds, confirm via `mcp__github__list_releases` that a new GitHub Release exists with:
  - `publish-api.tar.gz`
  - `publish-app.tar.gz`
  - `sbom-api.cdx.json`
  - three `.cosign.bundle` files
  - `sha256-manifest.txt`

  Optionally verify the cosign signature locally:

  ```bash
  cosign verify-blob \
    --certificate-identity-regexp 'https://github.com/tommimarkus/sisu-raidcal/.github/workflows/deploy.yml@.*' \
    --certificate-oidc-issuer 'https://token.actions.githubusercontent.com' \
    --bundle publish-api.cosign.bundle \
    publish-api.tar.gz
  ```

## Deferred — revisit when conditions change

- **Dependabot alerts.** Not enabled for now. Revisit once the project stabilizes. Enable at **Settings → Code security → Dependabot alerts**. Tracked in `SECURITY.md` under Known Limitations.

- **`CODEOWNERS`.** Deferred — solo maintainer, no review flow to gate. Revisit if collaborators are added.

- **`read_only: true` on docker-compose services.** The devsecops-audit quick-mode pass on the remediation branch surfaced `docker.HC-4` (warn) against `cosmosdb`, `azurite`, and `functions` in both `docker-compose.local.yml` and `docker-compose.test.yml`. Adding `read_only: true` requires mounting writable paths as `tmpfs:` and verifying the full E2E stack still boots. Defer until a dedicated compose hardening pass.

- **`USER app` directive in `api/Dockerfile`.** Accepted as out-of-scope for this phase because the Azure Functions `dotnet-isolated` prebuilt runtime manages privileges internally and adding the .NET 8+ `USER app` pattern requires `chown -R app:app /home` plus port-80 rebinding. Documented in `SECURITY.md` Known Limitations. Re-evaluate if the project moves off the prebuilt Functions image.

- **SLSA Build Level 2 (provenance attestation).** Removed from the plan on cost grounds — the `slsa-framework/slsa-github-generator` reusable workflow spins up a second runner per push (~2-3 min). SLSA Build L1 (declared, evidenced via cosign + SBOM + SHA manifest) is the current target. Upgrade when the Actions-minutes budget relaxes.

## Not applicable / platform-constrained

Documented in `SECURITY.md` Known Limitations; listed here for completeness.

- **Branch protection.** GitHub Free private repos do not enforce rulesets. Compensated by the workflow chain (Phase 2), signed commits (first user action above), and the pre-push hook (Phase 1.6).
- **Code Scanning / CodeQL.** Requires GitHub Advanced Security. Compensated by the PSRule SARIF artifact plus the in-run step summary added in Phase 2.2.
- **Hardware-key MFA.** TOTP MFA is active and accepted for a solo hobby project.
- **SWA deploy OIDC.** `Azure/static-web-apps-deploy` does not yet support OIDC. `SWA_DEPLOYMENT_TOKEN` rotated quarterly.
- **CI/CD audit event forwarding to SIEM.** No budget for off-platform event forwarding. GitHub Actions retains run logs for the default retention window.

## Audit item status

Reference: `docs/security-reviews/2026-04-14-devsecops-audit.md` §12.

| # | Audit code | Status |
|---|---|---|
| 1 | `CICD-SEC-1` / `DSO-HC-4` partial | Compensated + documented |
| 2 | `DSO-HC-10` | Closed (Phase 0) |
| 3 | `DSO-HC-0` | Closed (Phase 0) |
| 4 | `DSO-HC-11` (signing) | Closed (Phase 3) |
| 4 | `DSO-HC-12` (SARIF ingest) | Platform-constrained; compensated by step summary |
| 5 | Dependabot alerts | Deferred |
| 5 | Code Scanning default setup | Platform-constrained |
| 6 | `DSO-HC-2` (NuGet) | Closed (Phase 1.1) |
| 7 | `docker.HC-2` (Dockerfile/compose digests) | Closed (Phase 1.2, 1.3) |
| 8 | `docker.HC-1` (Dockerfile USER) | Documented as accepted (Phase 1.2) |
| 9 | `DSO-HC-7` (SWA token) | Documented (Phase 0) |
| 10 | pre-commit opt-in | Documented (Phase 0) |
| 11 | `DSO-SUB-8` (e2e path filter) | Subsumed by Phase 2 workflow chain |
| 12 | HSTS in SWA | Closed (Phase 1.4) |
| 13 | OpenAPI route inventory | Closed (Phase 4.2) |
| 14 | Threat models | Closed (Phase 4.1) |

## Unplanned work completed during execution

- **CVE patch: `System.Security.Cryptography.Xml` 10.0.5 → 10.0.6** in `tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj`. Addresses CVE-2026-33116 (GHSA-37gx-xxp4-5rgx) and CVE-2026-26171 (GHSA-w3x6-4m5h-cxqf), both High-CVSS DoS advisories in `EncryptedXml`. Surfaced transitively via `Microsoft.AspNetCore.DataProtection`. Pinned explicitly in the test csproj, lockfile regenerated.

- **HotReload lockfile consistency fix.** Blazor WebAssembly project lockfile exhibits non-determinism around `Microsoft.DotNet.HotReload.WebAssembly.Browser` (implicit SDK package). First lockfile generation missed it; `--force-evaluate` added it. Committed as `0fc5495`.

- **`CycloneDX` action swap.** Phase 3 replaced the Node 12-based `CycloneDX/gh-dotnet-generate-sbom` action with the direct `CycloneDX` dotnet tool (`dotnet tool install --global CycloneDX && dotnet-CycloneDX lfm.sln -F Json ...`). The original action would have failed on modern GitHub Actions runners due to Node 12 deprecation.
