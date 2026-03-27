# Infra Verification Posture

- Date: 2026-03-26
- Scope: `infra/modules/storage.bicep`, `infra/scripts/verify-security.sh`, and local verification wiring

## Posture

`storage.bicep` intentionally keeps `networkAcls.defaultAction` set to `Allow`.

## Rationale

The Function App on the current Consumption plan reads blobs directly and cannot use the `AzureServices` bypass for data-plane access without VNet integration. The storage account still blocks public blobs via `allowBlobPublicAccess: false`, so the effective posture is private containers with network ACLs left open to preserve app functionality. `verify-security.sh` is aligned to this intended posture so the verifier checks the deployed reality instead of a stricter deny-list that the app cannot currently satisfy.
