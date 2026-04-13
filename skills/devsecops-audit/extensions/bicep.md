# Extension — Bicep (Azure IaC)

**Applies to:** `infra/**/*.bicep`, `infra/**/*.bicepparam`, `main.bicep`, any `.bicep` file in the repo.

**Detection signals:**

- Path matches `**/*.bicep` or `**/*.bicepparam`.
- Any file whose first non-comment line contains `targetScope`, `resource`, `module`, `param`, `var`, or `output`.

**Applies to rubric sections:** §3 (Deploy stage), §5.1 items 1, 17, 19, §5.3 items 2, 4, and the WAF pillars from `CLAUDE.md` § "Infrastructure Development".

## Cost banding

This extension splits smells into two bands:

- **Band 1 — always-block.** Remediation is free or near-free. Fires under all cost stances.
- **Band 2 — cost-gated.** Remediation requires a paid Azure SKU. Fires only when the resolved cost stance is `full` or `mixed` with the specific code enabled.

The cost-stance resolver is `../references/procedures/cost-stance-detection.md`.

## Band 1 — always-block

### `bicep.HC-1` — Shared keys / connection strings instead of managed identity

**Pattern:** a `listKeys(...)` call, a `connectionString` property, or `accountKey` / `primaryKey` / `masterKey` in a module output or resource property where a managed identity is feasible (Cosmos, Storage, Service Bus, Key Vault, Event Grid, App Configuration, SignalR).

**Detection (ripgrep):**
```
listKeys\(|connectionString\s*:|accountKey\s*:|primaryKey\s*:|masterKey\s*:
```

**Severity:** `block`

**Remediation action:**
> Replace with a system-assigned or user-assigned managed identity. Grant the caller role-based access (e.g. `Cosmos DB Built-in Data Contributor`, `Storage Blob Data Contributor`). Remove the key-based path entirely.

### `bicep.HC-2` — TLS < 1.2

**Pattern:** `minimumTlsVersion` set to `TLS1_0`, `TLS1_1`, `1.0`, or `1.1`. Or the property missing on a resource type that supports it (App Service, Storage, SQL, Cosmos — but **not** required on resource types that default to ≥ 1.2 like Functions Flex Consumption).

**Detection (ripgrep):**
```
minimumTlsVersion\s*:\s*['"]?(TLS1_0|TLS1_1|1\.0|1\.1)
```

**Severity:** `block`

**Remediation action:**
> Set `minimumTlsVersion: 'TLS1_2'` (App Service / Storage / SQL) or `minTlsVersion: 'TLS1_2'` (Cosmos). Verify downstream clients support it before merging.

### `bicep.HC-3` — Local auth enabled

**Pattern:** any of:
- Cosmos: `disableLocalAuth` absent or `false`
- Storage: `allowSharedKeyAccess: true` or absent
- Service Bus: `disableLocalAuth: false`
- Event Grid: `disableLocalAuth: false`
- App Configuration: `disableLocalAuth: false`

**Detection (ripgrep, per resource type):**
```
allowSharedKeyAccess\s*:\s*true
disableLocalAuth\s*:\s*false
```

**Severity:** `block`

**Remediation action:**
> Set the local-auth-disable flag to the restrictive value. Grant the caller an appropriate RBAC role. Verify no other consumer of the resource still uses shared-key auth.

### `bicep.HC-4` — FTP / basic auth on App Service

**Pattern:** an `Microsoft.Web/sites` resource without `siteConfig.ftpsState: 'Disabled'` or without `basicPublishingCredentialsPolicies` disabled.

**Detection:** walk the module for `Microsoft.Web/sites` and verify the block contains `ftpsState: 'Disabled'` and a sibling resource of type `Microsoft.Web/sites/basicPublishingCredentialsPolicies` with `allow: false`.

**Severity:** `block`

**Remediation action:**
> Disable FTP and basic publishing credentials. Deployment should go through OIDC-federated Run From Package or ZIP deploy with RBAC.

### `bicep.HC-5` — Secrets in params instead of Key Vault references

**Pattern:** a `param` declared as `@secure() param <name> string` that is used as a direct value in a resource property, without a Key Vault reference wrapper.

**Detection:** grep for `@secure()` in params, then cross-reference the usage site. If the value is passed directly to `appSettings` or `connectionStrings` without a `Microsoft.KeyVault(...)` reference form, it's a smell.

**Severity:** `block`

**Remediation action:**
> Move the secret to Key Vault. Reference it from Bicep via `@Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/<secret>/)` in the target resource's settings block.

### `bicep.HC-6` — Missing `CanNotDelete` lock on stateful resources

**Pattern:** a module creating Cosmos DB account, Storage account, Key Vault, SQL server, or any stateful resource, without a corresponding `Microsoft.Authorization/locks` sub-resource with `level: 'CanNotDelete'`.

**Detection:** for each stateful resource type, check whether a locks sub-resource exists in the same module.

**Severity:** `block`

**Remediation action:**
> Add a `Microsoft.Authorization/locks` resource scoped to the stateful resource with `level: 'CanNotDelete'`. Required by CLAUDE.md § Infrastructure Development / Reliability pillar.

### `bicep.HC-7` — Soft delete / purge protection disabled

**Pattern:** Key Vault with `enableSoftDelete: false` or `enablePurgeProtection` missing/false; Storage with `deleteRetentionPolicy.enabled: false`; Cosmos with no backup policy configured.

**Detection (per resource type):**
```
enableSoftDelete\s*:\s*false
enablePurgeProtection\s*:\s*false
deleteRetentionPolicy[\s\S]*?enabled\s*:\s*false
```

**Severity:** `block`

**Remediation action:**
> Enable soft delete and purge protection on Key Vault. Enable blob soft delete with 7+ day retention on Storage. Configure Cosmos continuous backup.

### `bicep.HC-8` — Diagnostic settings missing

**Pattern:** any resource that supports `Microsoft.Insights/diagnosticSettings` without one configured. Relevant for Cosmos, Storage, Key Vault, App Service, Functions, Application Gateway, Front Door.

**Detection:** for each resource of a type that supports diagnostic settings, check whether a same-scope `Microsoft.Insights/diagnosticSettings` sub-resource exists.

**Severity:** `block`

**Rubric note:** CLAUDE.md § Cost Guidance explicitly grants the free 5 GB/month LAW ingestion — this remediation is free within the declared budget.

**Remediation action:**
> Add a `Microsoft.Insights/diagnosticSettings` resource targeting the existing Log Analytics workspace. Enable `AllLogs` and `AllMetrics` category groups.

### `bicep.HC-9` — Hardcoded names / regions / domains

**Pattern:** a string literal in a module that is (a) a resource name (e.g. `'lfm-prod-cosmos'`), (b) a region (`'eastus'`, `'westeurope'`), (c) a domain (`'example.com'`), or (d) a numeric resource ID.

**Detection:** grep for resource-name patterns and region names in `.bicep` files, excluding `main.bicep` where the parent composition is allowed to pass them in.

**Severity:** `block`

**Remediation action:**
> Promote to a `param` with a `@description` and, where applicable, `@minLength` / `@maxLength`. Values come from `parameters.example.json` or `deploy-infra.yml` inline overrides (see CLAUDE.md § Workflow parameterization).

### `bicep.HC-10` — `http20Enabled: false` or client affinity on stateless APIs

**Pattern:** an `Microsoft.Web/sites` resource with `siteConfig.http20Enabled: false` or `clientAffinityEnabled: true` where the API is stateless (which, for Functions, is always).

**Detection (ripgrep):**
```
http20Enabled\s*:\s*false
clientAffinityEnabled\s*:\s*true
```

**Severity:** `block` (HTTP/2) / `warn` (affinity)

**Remediation action:**
> Set `http20Enabled: true` and `clientAffinityEnabled: false`. WAF Performance pillar per CLAUDE.md.

### `bicep.HC-11` — Param missing metadata

**Pattern:** a `param` declaration with no `@description`, or a string param that accepts a bounded length without `@minLength` / `@maxLength`, or a numeric param accepting a bounded range without `@minValue` / `@maxValue`.

**Detection:** parse `param` declarations and check for the decorator annotations on preceding lines.

**Severity:** `warn`

**Remediation action:**
> Add `@description('...')` for every param. Add length/value bounds where Azure enforces them. Required by CLAUDE.md § Operational Excellence pillar.

## Band 2 — cost-gated

Each Band 2 code fires only when the resolved cost stance is `full`, or `mixed` with the specific code listed in `mixedEnabled`. Under `free`, the skill emits one `info` suppression line for this extension and does not evaluate these codes individually.

### `bicep.B2-1` — Defender for Cloud Standard tier absent

**Pattern:** no `Microsoft.Security/pricings` resource at subscription scope with `pricingTier: 'Standard'` for the relevant plans.

**Severity:** `warn` (block under `full` stance)

### `bicep.B2-2` — Private Link / private endpoints absent

**Pattern:** stateful resources (Cosmos, Storage, Key Vault) with `publicNetworkAccess: 'Enabled'` or no `Microsoft.Network/privateEndpoints` companion.

**Severity:** `warn`

### `bicep.B2-3` — Multi-region active-active absent

**Pattern:** single-region Cosmos DB (no `locations` array with ≥ 2 entries) or single-region Functions app.

**Severity:** `warn`

### `bicep.B2-4` — WAF on App Gateway / Front Door Premium absent

**Pattern:** no `Microsoft.Network/ApplicationGatewayWebApplicationFirewallPolicies` or no `Microsoft.Cdn/profiles` with `Standard_AzureFrontDoor_Premium` SKU fronting public endpoints.

**Severity:** `warn`

### `bicep.B2-5` — HSM-backed / Premium Key Vault with CMK absent

**Pattern:** Key Vault with `sku.name: 'standard'` where customer-managed keys are required.

**Severity:** `warn`

### `bicep.B2-6` — Azure DDoS Protection Standard absent

**Pattern:** no `Microsoft.Network/ddosProtectionPlans` at the VNet.

**Severity:** `warn`

## Positive signals

### `bicep.POS-1` — Managed identity usage

**Pattern:** resources with `identity: { type: 'SystemAssigned' }` or `type: 'UserAssigned'`, plus corresponding `Microsoft.Authorization/roleAssignments` granting RBAC.

### `bicep.POS-2` — Key Vault references in app settings

**Pattern:** `@Microsoft.KeyVault(SecretUri=...)` syntax used in `appSettings` / `connectionStrings`.

### `bicep.POS-3` — Soft delete / purge protection enabled

**Pattern:** Key Vault with both `enableSoftDelete: true` and `enablePurgeProtection: true`.

### `bicep.POS-4` — Diagnostic settings within free grant

**Pattern:** diagnostic settings targeting a Log Analytics workspace that has `dailyQuotaGb` set at or below the 5 GB free grant (5 or less).

### `bicep.POS-5` — Fully parameterized modules

**Pattern:** a module with zero hardcoded names, regions, or domains; all environment-specific values come through params.

## Carve-outs

- **Do not flag `bicep.HC-9` in `main.bicep`** for values passed as inline overrides in `deploy-infra.yml` — the composition root is allowed to receive hardcoded values from workflow variables per CLAUDE.md § Workflow parameterization.
- **Do not flag `bicep.HC-11` `@minLength` / `@maxLength` for params that accept free-form strings with no Azure-enforced bounds** (e.g. `@description`-style documentation strings).
- **Do not flag Band 2 codes when the resolved cost stance is `free`** — emit the single `info` suppression line instead.
