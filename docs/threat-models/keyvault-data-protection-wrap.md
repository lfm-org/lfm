# Threat Model: Key Vault Data Protection Key Wrap

## Introduction

This document covers the trust boundary between the Azure Functions app and Azure Key
Vault, where an RSA key named `dataprotection` wraps the ASP.NET Core Data Protection
key ring stored in a blob. The Data Protection keys are the root secret for all session
cookie encryption and the OAuth `login_state` cookie. An attacker who can extract,
rotate, or block access to this Key Vault key can forge session cookies, forge OAuth
state, decrypt existing cookies, or deny authentication to all users.

The boundary now also covers two adjacent KV-resolved secrets that share the same role
assignment: `site-admin-battle-net-ids` (read by `KeyVaultSecretResolver` /
`SiteAdminService`) and the `Blizzard__ClientId` / `Blizzard__ClientSecret` pair
(resolved by the Functions platform via `@Microsoft.KeyVault(...)` references).

## Data Flow

```
Functions Instance          Azure Blob Storage           Azure Key Vault
       |                           |                            |
       | startup / key refresh     |                            |
       |--PersistKeysToAzureBlobStorage()-->                    |
       |  (read existing XML or create new)                     |
       |                           |--GET keys.xml blob-------->|
       |                           |<--encrypted XML blob-------|
       |--ProtectKeysWithAzureKeyVault()----------------------->|
       |  (unwrap AES key inside XML)                           |
       |<--unwrapped key material--------------------------------|
       |  (DP key ring in memory)                               |
       |                                                        |
       | per-request (Protect/Unprotect session cookie)         |
       |--IDataProtector.Protect(payload)                       |
       |  (uses in-memory key ring — NO per-request KV call)    |
       |                                                        |
       | admin allowlist refresh (every 10s cache TTL)          |
       |--SecretClient.GetSecretAsync("site-admin-battle-net-ids")->|
       |<--secret value-----------------------------------------|
       |                                                        |
       | platform startup (slot resolves @Microsoft.KeyVault)   |
       |   for Blizzard__ClientId / __ClientSecret              |
       |                                                        |
       | key rotation (90-day lifetime)                         |
       |--new key generated, wrapped with KV RSA key----------->|
       |--new encrypted XML written to blob----------------->   |
```

## Trust Boundaries Crossed

- **Functions MI to Key Vault**: `DefaultAzureCredential` presents the system-assigned
  managed identity token; Key Vault RBAC (`Key Vault Secrets User` role) controls
  access. The RSA wrap/unwrap operations (`wrapKey`, `unwrapKey`) are the privileged
  cryptographic operations; secret reads (`get`) are the privileged secret operations.
- **Functions MI to Blob Storage**: the key ring XML blob is persisted to and read from
  a storage account; blob access uses the Functions MI via `Blob Data Owner` role on
  the storage account scope.
- **Key ring in memory to Data Protection API**: once unwrapped at startup, the key
  material lives in the process memory of each Functions instance; memory-level
  isolation is the only boundary.

## STRIDE Table

| Category | Threat | Current mitigation | Residual risk |
|---|---|---|---|
| **Spoofing** | Attacker spoofs the Functions identity to obtain `unwrapKey` permission on the Key Vault key. | System-assigned managed identity; no client secret to leak. RBAC scoped to the Key Vault resource. Token issuance and validation handled by Azure AD. | Low — requires compromise of the Azure control plane or the Functions runtime itself. |
| **Tampering** | Attacker overwrites the blob containing the encrypted key ring XML to inject a known key. | Blob storage account has `allowSharedKeyAccess: false` (`infra/modules/storage.bicep:24`), so all writes go through RBAC; the only role grant is `Blob Data Owner` on the Functions MI. Key Vault `enablePurgeProtection: true` prevents deletion of the wrapping key. CanNotDelete lock prevents accidental KV removal. | Medium — the **same** `Blob Data Owner` role grant covers the DP key blob, the `AzureWebJobsStorage` runtime blobs, and the `wow` reference-data container. A compromised Functions MI can tamper with all three; there is no per-container blob RBAC scoping today. There is no blob integrity check (e.g. a KV-signed hash) beyond Key Vault wrapping. |
| **Repudiation** | Attacker extracts a Data Protection key and later claims the session was forged by the application. | Key Vault diagnostic settings (`categoryGroup: audit`) stream every `wrapKey` / `unwrapKey` / secret-read call with caller identity to Log Analytics. | Low — KV audit log provides non-repudiation for KV operations; no log of which sessions were issued using which key. |
| **Information disclosure** | Attacker reads the encrypted key ring blob and separately obtains the KV private key material to decrypt it offline. | KV key operations are server-side: the private RSA key never leaves Key Vault. The `unwrapKey` operation returns only the symmetric AES key. Standard SKU (software-protected at rest); no HSM. | Medium — standard SKU means the RSA private key is software-protected, not in an HSM. A sufficiently privileged Azure principal with `Key Vault Crypto Officer` or higher could export the key. |
| **Information disclosure — secret blast radius** | The single `Key Vault Secrets User` role grant on the Functions MI now covers more than the `dataprotection` key: it also reads `site-admin-battle-net-ids` (via `KeyVaultSecretResolver` → `SiteAdminService`), resolves `battlenet-client-id` / `battlenet-client-secret` (via `@Microsoft.KeyVault(...)` app-setting references), and resolves `audit-hash-salt` for the audit-log HMAC. | Same RBAC role assignment governs all of them; Azure RBAC does not support per-secret scoping under a single role. All are intended to be read by the Functions MI. | Medium — a compromised Functions MI gains read access to **every current and future secret** in the vault, not just the wrapping key. New secrets added to this vault inherit the same exposure. |
| **Denial of service** | Attacker revokes the Functions MI's Key Vault access or deletes/disables the `dataprotection` key, preventing session establishment. | CanNotDelete resource lock on the Key Vault. `enablePurgeProtection: true` prevents immediate permanent deletion. Key is configured for `wrapKey`/`unwrapKey` only. | Medium — a control-plane action (role assignment removal) could revoke access without triggering the lock. Existing in-memory key rings would continue to work until the Functions instance restarts. |
| **Elevation of privilege** | Attacker obtains the in-memory Data Protection key (e.g., via memory dump, process injection) and uses it to forge session cookies for arbitrary users. | Key ring lives in process memory only after the `unwrapKey` call at startup/rotation; never persisted in cleartext. Functions is a managed PaaS service. `SetApplicationName("Lfm")` scopes the protector so keys are not usable across applications. Child protectors split purposes (`Lfm.Session.v1`, `Lfm.OAuth.LoginState.v1`). | High — once a Functions instance is running, the symmetric key is in memory. A sufficiently privileged attacker with remote code execution on the Functions host can extract it. This is the highest residual risk on this boundary. |

## Key Code References

- `api/Program.cs:174` — `KeyVaultSecretResolver` registered as `ISecretResolver`
  singleton; resolves admin allowlist secret.
- `api/Program.cs:175` — `SiteAdminService` (consumer) registered.
- `api/Program.cs:241-276` — Data Protection setup block.
  - `:257-259` — `AddDataProtection()` with `SetApplicationName("Lfm")` and 90-day key
    lifetime.
  - `:261-268` — production path: `PersistKeysToAzureBlobStorage` (URI from
    `Storage__DataProtectionBlobUri`) + `ProtectKeysWithAzureKeyVault` (URI from
    `Auth__DataProtectionKeyUri`) using `DefaultAzureCredential`. The KV key URI is
    versionless to allow automatic key rotation.
  - `:270-276` — local/E2E fallback: keys persisted to `$TMPDIR/lfm-dp-keys` with no
    encryption; ephemeral sessions acceptable for dev/test.
- `api/Program.cs:286-287` — `AuditLog.ConfigureHasher` installs the DI-selected
  `IActorHasher` (HMAC when `AuditOptions.HashSalt` is set, identity otherwise) so
  every audit event hashes the actor id before reaching App Insights.
- `api/Auth/DataProtectionSessionCipher.cs:11-12` — child `IDataProtector` with
  purpose `Lfm.Session.v1`; root ring key is unwrapped at startup, not per-call.
- `api/Services/BlizzardOAuthClient.cs:40` — `login_state` cookie uses a separate
  child protector with purpose `Lfm.OAuth.LoginState.v1`; same root key ring,
  different purpose isolation.
- `api/Services/KeyVaultSecretResolver.cs` — singleton `SecretClient` constructed with
  `DefaultAzureCredential`; reads `site-admin-battle-net-ids`.
- `api/Services/SiteAdminService.cs` — 10-second in-memory cache of admin id list;
  fail-open-to-empty on KV error.
- `infra/modules/keyvault.bicep:16-39` — Key Vault provisioned with
  `enableRbacAuthorization`, `enableSoftDelete`, `enablePurgeProtection`,
  `softDeleteRetentionInDays: 90`.
- `infra/modules/keyvault.bicep:42-49` — `CanNotDelete` management lock.
- `infra/modules/keyvault.bicep:51-59` — diagnostic settings (`categoryGroup: audit`)
  forwarding KV operation logs to Log Analytics.
- `infra/modules/keyvault.bicep:62-70` — `dataprotection` RSA-2048 key restricted to
  `wrapKey` and `unwrapKey` operations only.
- `infra/modules/keyvault.bicep:74` — versionless key URI output used by
  `functions.bicep` as `dataProtectionKeyUri`.
- `infra/modules/storage.bicep:23-26` — `allowSharedKeyAccess: false`,
  `allowBlobPublicAccess: false`, `minimumTlsVersion: 'TLS1_2'`,
  `supportsHttpsTrafficOnly: true` — closes the shared-key write path on the storage
  account that holds the DP key blob.
- `infra/modules/functions.bicep:118` — `Auth__DataProtectionKeyUri` app setting.
- `infra/modules/functions.bicep:122` — `Storage__DataProtectionBlobUri` app setting.
- `infra/modules/functions.bicep:144-152` — `Key Vault Secrets User` role
  (`4633458b-17de-408a-b874-0445c86b69e6`) granted to the Functions MI on the KV
  scope. **One role assignment, vault-wide; per-secret scoping is not possible under
  RBAC.**

## Operator prerequisite (the gap is now in operator action, not Bicep)

`AuditOptions.HashSalt` is bound from the `Audit` configuration section
(`Program.cs:83-84`) and wired via a `@Microsoft.KeyVault(...)` reference to
the `audit-hash-salt` secret in `infra/modules/functions.bicep:134`. Bicep
references the secret but does **not** create it — operators must populate
`audit-hash-salt` (e.g., `openssl rand -base64 32`) in the Key Vault before
the first deploy. Until the secret exists the Functions runtime resolves the
app setting as empty and `IActorHasher` falls back to `IdentityActorHasher`
— deployed environments will emit plaintext `battleNetId` (PII) into App
Insights. See `docs/security-architecture.md` for the full deploy-prerequisite
list. See `audit-log-pii-pipeline.md` (backlog) for the full pipeline view.

## Out of Scope

- Network-level Key Vault access controls (VNet service endpoints, private endpoints)
  — not applicable at Consumption plan free tier; `defaultAction: Allow` is a known
  accepted trade-off documented in `keyvault.bicep`.
- Key Vault Premium / HSM upgrade path for hardware-protected key material.
- Rotation procedures and runbook for manual emergency key rotation after a suspected
  key compromise.
- The admin allowlist authorization itself (cache TTL, fail-open behaviour) — covered
  by `admin-privilege-boundary.md`.
