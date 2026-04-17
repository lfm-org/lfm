# Threat Model: Key Vault Data Protection Key Wrap

## Introduction

This document covers the trust boundary between the Azure Functions app and Azure Key
Vault, where an RSA key named `dataprotection` wraps the ASP.NET Core Data Protection
key ring stored in a blob. The Data Protection keys are the root secret for all session
cookie encryption and the OAuth `login_state` cookie. An attacker who can extract,
rotate, or block access to this Key Vault key can forge session cookies, forge OAuth
state, decrypt existing cookies, or deny authentication to all users.

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
       | key rotation (90-day lifetime)                         |
       |--new key generated, wrapped with KV RSA key----------->|
       |--new encrypted XML written to blob----------------->   |
```

## Trust Boundaries Crossed

- **Functions MI to Key Vault**: `DefaultAzureCredential` presents the system-assigned
  managed identity token; Key Vault RBAC (`Key Vault Secrets User` role) controls
  access. The RSA wrap/unwrap operations (`wrapKey`, `unwrapKey`) are the privileged
  operations.
- **Functions MI to Blob Storage**: the key ring XML blob is persisted to and read from
  a storage account; blob access uses the Functions MI via `Blob Data Owner` role.
- **Key ring in memory to Data Protection API**: once unwrapped at startup, the key
  material lives in the process memory of each Functions instance; memory-level
  isolation is the only boundary.

## STRIDE Table

| Category | Threat | Current mitigation | Residual risk |
|---|---|---|---|
| **Spoofing** | Attacker spoofs the Functions identity to obtain `unwrapKey` permission on the Key Vault key. | System-assigned managed identity; no client secret to leak. RBAC is scoped to the specific Key Vault resource in Bicep. Token issuance and validation is handled by Azure AD. | Low — requires compromise of the Azure control plane or the Functions runtime itself. |
| **Tampering** | Attacker overwrites the blob containing the encrypted key ring XML to inject a known key. | Blob storage is accessed only by the Functions MI (`Blob Data Owner`). Key Vault `enablePurgeProtection: true` prevents deletion of the wrapping key. CanNotDelete lock prevents accidental KV removal. | Medium — an attacker with Storage `Blob Data Owner` access (e.g. via compromised MI) could replace the blob. There is no blob integrity check (e.g. KV-signed hash) beyond the Key Vault wrapping. |
| **Repudiation** | Attacker extracts a Data Protection key and later claims the session was forged by the application. | Key Vault audit logs (`categoryGroup: audit`) are streamed to Log Analytics via diagnostic settings. Key Vault access logs record every `unwrapKey` and `wrapKey` operation with caller identity. | Low — KV audit log provides non-repudiation for KV operations; no log of which sessions were issued using which key. |
| **Information disclosure** | Attacker reads the encrypted key ring blob and separately obtains the KV private key material to decrypt it offline. | KV key operations are server-side: the private RSA key never leaves Key Vault. The `unwrapKey` operation returns only the symmetric AES key, not the RSA private key. Key Vault HSM is not used (standard SKU) — key material is software-protected at rest. | Medium — standard SKU means the RSA private key is software-protected, not in an HSM. A sufficiently privileged Azure principal with `Key Vault Crypto Officer` or higher could export the key. |
| **Denial of service** | Attacker revokes the Functions MI's Key Vault access or deletes/disables the `dataprotection` key, preventing session establishment. | CanNotDelete resource lock on the Key Vault (`kvLock`). `enablePurgeProtection: true` prevents immediate permanent deletion. Key is configured for `wrapKey`/`unwrapKey` only. | Medium — a control-plane action (role assignment removal) could revoke access without triggering the lock. Existing in-memory key rings would continue to work until the Functions instance restarts. |
| **Elevation of privilege** | Attacker obtains the in-memory Data Protection key (e.g., via memory dump, process injection) and uses it to forge session cookies for arbitrary users. | Key ring lives in process memory only after the `unwrapKey` call at startup/rotation; it is not persisted in cleartext anywhere. Functions is a managed PaaS service. `SetApplicationName("Lfm")` scopes the protector so keys are not usable across different applications. | High — once a Functions instance is running, the symmetric key is in memory. A sufficiently privileged attacker with remote code execution on the Functions host can extract it. This is the highest residual risk in this boundary. |

## Key Code References

- `api/Program.cs:167-169` — `AddDataProtection()` with `SetApplicationName("Lfm")`
  and `SetDefaultKeyLifetime(90 days)`.
- `api/Program.cs:171-178` — production path: `PersistKeysToAzureBlobStorage` +
  `ProtectKeysWithAzureKeyVault` using `DefaultAzureCredential`; the KV key URI is
  versionless to allow automatic key rotation.
- `api/Program.cs:179-186` — local/E2E fallback: keys persisted to `$TMPDIR/lfm-dp-keys`
  with no encryption; ephemeral sessions acceptable for dev/test.
- `api/Auth/DataProtectionSessionCipher.cs:8-9` — creates a child `IDataProtector`
  with purpose `Lfm.Session.v1`; the root ring key is unwrapped at startup, not per-call.
- `api/Services/BlizzardOAuthClient.cs:37` — `login_state` cookie uses a separate child
  protector with purpose `Lfm.OAuth.LoginState.v1`; same root key ring, different
  purpose isolation.
- `infra/modules/keyvault.bicep:13-37` — Key Vault provisioned with `enableRbacAuthorization`,
  `enableSoftDelete`, `enablePurgeProtection`, `softDeleteRetentionInDays: 90`.
- `infra/modules/keyvault.bicep:39-46` — `CanNotDelete` management lock on the Key Vault.
- `infra/modules/keyvault.bicep:59-67` — `dataprotection` RSA-2048 key restricted to
  `wrapKey` and `unwrapKey` operations only.
- `infra/modules/keyvault.bicep:71` — versionless key URI output used by `functions.bicep`
  as `dataProtectionKeyUri`.
- `infra/modules/functions.bicep:132-141` — `Key Vault Secrets User` role
  (`4633458b-17de-408a-b874-0445c86b69e6`) granted to the Functions MI on the KV scope.
- `infra/modules/functions.bicep:115` — `Auth__DataProtectionKeyUri` app setting
  receives the versionless KV key URI via Bicep parameter.

## Out of Scope

- Network-level Key Vault access controls (VNet service endpoints, private endpoints) —
  not applicable at Consumption plan free tier; `defaultAction: Allow` is a known
  accepted trade-off documented in `keyvault.bicep`.
- Key Vault Premium / HSM upgrade path for hardware-protected key material.
- Rotation procedures and runbook for manual emergency key rotation after a suspected
  key compromise.
