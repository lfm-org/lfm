# Extension — .NET Security

**Applies to:** C# source files (`*.cs`), project files (`*.csproj`, `*.props`), and settings files (`appsettings*.json`, `local.settings.json`) under `api/`, `app/`, `shared/`, `tests/`.

**Detection signals:**

- Any `*.csproj` or `*.sln` in the audit target.
- Any `*.cs` file under the covered directories.
- Any `appsettings*.json` or `local.settings.json`.

**Applies to rubric sections:** §5.1 items 1, 16, 20, §5.3 item 5 (developer-facing), §6 items 3, 4, 5, 6 (code-level subtle failures).

**Complementary skills:** `test-quality-audit` owns test quality in these same files; this extension is the **security lens**, not a general code review. Findings here should focus on security-relevant patterns, not style, complexity, or non-security bugs.

## Smell codes

### `dns.HC-1` — Secret material in `appsettings*.json` / `.csproj` / `*.props`

**Relationship to core:** this code is the .NET-specific extension of core `DSO-HC-1` (secrets in source control). Core `DSO-HC-1` applies to any file; `dns.HC-1` adds .NET-file-specific patterns and places the finding under the `dotnet-security` extension's remediation flow. When both fire for the same file, cite `dns.HC-1` (more specific wins) and suppress the duplicate core finding.

**Pattern:** any of the following in a committed `.cs`, `.csproj`, `*.props`, `appsettings*.json`, or `local.settings.json` file:
- AWS access key shape (`AKIA[0-9A-Z]{16}`)
- Azure storage account key shape (`AccountKey=[A-Za-z0-9+/=]{88}`)
- GitHub PAT shape (`ghp_[A-Za-z0-9]{36}`)
- Slack token shape (`xox[baprs]-[A-Za-z0-9-]+`)
- Generic bearer token shape (long base64, > 20 chars, assigned to a key named `*Token*` / `*Secret*` / `*Password*`)
- Blizzard OAuth client secret shape (hex or mixed-case, assigned to `Blizzard__ClientSecret`)
- `Cosmos__Endpoint` or similar concatenated with `;AccountKey=`
- Any private key header (`-----BEGIN (RSA |EC |OPENSSH |)PRIVATE KEY-----`)

**Detection (per-pattern ripgrep):**
```
rg -nE 'AKIA[0-9A-Z]{16}' <path>
rg -nE 'AccountKey=[A-Za-z0-9+/=]{88}' <path>
rg -nE 'ghp_[A-Za-z0-9]{36}' <path>
rg -nE 'xox[baprs]-[A-Za-z0-9-]+' <path>
rg -nE 'Blizzard__ClientSecret\s*[:=]\s*["''][^"'']+' <path>
rg -nE '^-----BEGIN (RSA |EC |OPENSSH |)PRIVATE KEY-----' <path>
```

Any real-shape credential in a committed file is a smell — **including** placeholder values shaped like real credentials in `.env.example` or `appsettings.Example.json`.

**Severity:** `block`

**Rubric:** devsecops.md §5.1.1; CICD-SEC-6.

**Remediation action:**
> Rotate the credential immediately (assume public compromise). Move to Key Vault. Reference in `appsettings.json` via `@Microsoft.KeyVault(SecretUri=...)` syntax at runtime or via managed identity for Azure SDK clients. Add a pre-commit secret-scan hook.

### `dns.HC-2` — `[AllowAnonymous]` on a non-public endpoint

**Pattern:** a `[AllowAnonymous]` attribute on a controller action, minimal-API endpoint, or HTTP-triggered function that is not in a documented public allow-list.

**Detection:** grep for `[AllowAnonymous]` in `api/` and `app/`, then cross-reference against an allow-list (or against `SECURITY.md` if one documents public endpoints).

**Severity:** `warn` (because false positives are common — not every `[AllowAnonymous]` is wrong)

**Rubric:** devsecops.md §5.1.10 (disclosure surface); §6.4 (BOLA-adjacent).

**Remediation action:**
> Verify the endpoint is intentionally public. If yes, add a comment linking to the design decision. If no, replace with `[Authorize]` and the appropriate scope / role check.

### `dns.HC-3` — Missing `[Authorize]` / `AuthorizationLevel.Function` on non-public endpoint

**Pattern:** an HTTP-triggered Azure Function without `AuthorizationLevel.Function` or higher (`AuthorizationLevel.Admin`), or a minimal-API endpoint mapped without a `RequireAuthorization()` call.

**Detection:**
```
[HttpTrigger\(.*?AuthorizationLevel\.Anonymous
```
Plus: grep for `MapGet` / `MapPost` / `MapPut` / `MapDelete` in `app/` / `api/` that are not followed by `.RequireAuthorization()`.

**Severity:** `block`

**Rubric:** devsecops.md §5.1.10; §6.3 (shadow APIs); §6.4 (BOLA).

**Remediation action:**
> For Functions: set `AuthorizationLevel.Function` (or `Admin` for ops endpoints). For minimal API: chain `.RequireAuthorization("policy-name")` with a specific policy that asserts per-object authz, not just "logged in."

### `dns.HC-4` — `AllowAnyOrigin()` with `AllowCredentials()`

**Pattern:** a CORS configuration that combines `.AllowAnyOrigin()` with `.AllowCredentials()`. The browser rejects this combination, so it's both broken **and** a security smell (nobody thought about it).

**Detection (ripgrep):**
```
AllowAnyOrigin\(\)[\s\S]*?AllowCredentials\(\)
```
Or the reverse order.

**Severity:** `block`

**Rubric:** devsecops.md §5.1.20.

**Remediation action:**
> Replace `.AllowAnyOrigin()` with `.WithOrigins(<explicit list>)`. The origins should come from the `Cors__AllowedOrigins__0` config, not be hardcoded.

### `dns.HC-5` — Missing security headers middleware

**Pattern:** `Program.cs` or `Startup.cs` has no middleware equivalent to CSP, X-Frame-Options / XFO, HSTS, Referrer-Policy. Either via `app.UseSecurityHeaders()`, `app.Use(async ctx => ...)` that sets them, or a library like `NetEscapades.AspNetCore.SecurityHeaders`.

**Detection:** read `Program.cs` / `Startup.cs` and check for any of: `UseSecurityHeaders`, `Content-Security-Policy`, `X-Frame-Options`, `Strict-Transport-Security`, `NetEscapades`.

**Severity:** `warn` (because the skill can't know the app's threat model without context)

**Rubric:** devsecops.md §5.1.16; ASVS V14.

**Remediation action:**
> Add security headers middleware. For Blazor WASM, the headers must be set by the host (SWA `staticwebapp.config.json` or Functions middleware). For server Blazor or Functions HTTP, add middleware in `Program.cs`.

### `dns.HC-6` — Connection string via string concatenation

**Pattern:** a connection-string construction like `"AccountEndpoint=" + endpoint + ";AccountKey=" + key` rather than using `ConnectionStringBuilder` or config sections.

**Detection (ripgrep):**
```
"AccountEndpoint=".*\+.*"AccountKey="
```
Similar for SQL: `"Server=".*\+.*"Password="`.

**Severity:** `warn`

**Rubric:** devsecops.md §5.1.1 (ties into secret handling).

**Remediation action:**
> Use the SDK's credential-based client construction (managed identity preferred) or a `ConnectionStringBuilder`. Never concatenate secrets into strings that may end up in logs.

### `dns.HC-7` — Cosmos / Blob / Service Bus client with shared key

**Pattern:** `new CosmosClient(connectionString)` or `new BlobServiceClient(connectionString)` where a `TokenCredential`-based constructor is available and managed identity is feasible.

**Detection (ripgrep):**
```
new\s+CosmosClient\s*\(\s*.*ConnectionString
new\s+BlobServiceClient\s*\(\s*.*ConnectionString
new\s+ServiceBusClient\s*\(\s*.*ConnectionString
```

**Severity:** `warn`

**Rubric:** devsecops.md §5.3.2 (positive is OIDC / MI); §5.1.7.

**Remediation action:**
> Replace with `new CosmosClient(endpoint, new DefaultAzureCredential())`. Grant the identity the appropriate data-plane role (e.g. `Cosmos DB Built-in Data Contributor`).

### `dns.HC-8` — Authz denial log missing security-relevant fields

**Pattern:** a log statement on an authz denial path that omits actor identity, resource ID, decision, and trace ID. Specifically: `_logger.LogWarning("Access denied")` with no structured fields.

**Detection:** grep for `_logger.(LogWarning|LogError).*denied` and inspect arguments.

**Severity:** `warn`

**Rubric:** devsecops.md §6.6; CICD-SEC-10.

**Remediation action:**
> Log structured fields: `_logger.LogWarning("Access denied for user {UserId} on resource {ResourceId}: {Decision} (trace {TraceId})", userId, resourceId, decision, Activity.Current?.TraceId)`.

### `dns.LC-1` — Shadow / zombie function endpoint

**Pattern:** a `[Function]` or `[HttpTrigger]` handler registered in `api/Functions/` whose route does not appear in any OpenAPI / route inventory document.

**Detection:** enumerate `[HttpTrigger(...)]` route values in `api/Functions/*.cs`, cross-reference against any OpenAPI document or routing manifest. Unreferenced routes are candidate zombies.

**Severity:** `warn`

**Rubric:** devsecops.md §6.3; API9:2023.

**Remediation action:**
> Either document the endpoint in the OpenAPI / route inventory, or delete the handler if it's dead.

### `dns.LC-2` — Mass assignment / BOPLA risk

**Pattern:** a request-body DTO whose properties are a superset of the documented OpenAPI schema. Specifically: a property on the class that has no counterpart in the schema, and the class is used as a parameter on an HTTP-triggered function or API controller.

**Detection:** for each `[FromBody]` model, compare `public` settable properties against the OpenAPI definition (if present). Fields only in the model are candidates.

**Severity:** `warn`

**Rubric:** devsecops.md §6.5; API3:2023.

**Remediation action:**
> Either add the missing fields to the OpenAPI schema (if intentional) or add `[JsonIgnore]` / make the property `init`-only / remove the setter to prevent mass-assignment. Prefer explicit input DTOs that carry only the documented fields.

## Positive signals

### `dns.POS-1` — Managed identity client construction

**Pattern:** Azure SDK clients instantiated with a `TokenCredential` (e.g. `DefaultAzureCredential`, `ManagedIdentityCredential`).

### `dns.POS-2` — Key Vault reference resolution

**Pattern:** app settings resolved via `@Microsoft.KeyVault(SecretUri=...)` or via `AddAzureKeyVault(...)` in config builder.

### `dns.POS-3` — Explicit security headers middleware

**Pattern:** `Program.cs` or `Startup.cs` with explicit CSP / HSTS / XFO / Referrer-Policy configuration.

### `dns.POS-4` — `[Authorize(Roles=...)]` with specific role checks

**Pattern:** `[Authorize]` attributes that name specific roles or policies, not bare `[Authorize]` (which only asserts "logged in").

### `dns.POS-5` — OpenAPI-driven request models

**Pattern:** request DTOs generated from an OpenAPI source-of-truth, or validated against the schema at runtime via a library.

## Carve-outs

- **Do not flag `dns.HC-1` on files matching `*.Example.*`** — example files are expected to contain placeholder credentials. Still flag if the placeholder shape matches a real credential format exactly (that's `DSO-HC-1` under a different lens).
- **Do not flag `dns.HC-2` on endpoints explicitly listed in `SECURITY.md`** as public.
- **Do not flag `dns.HC-3` on Static Web Apps routes** — SWA handles auth at the edge via `routes.json`, not via `[Authorize]`.
- **Do not flag `dns.HC-5` on Blazor WASM projects** — the host (SWA / Functions) is responsible for headers, not the WASM client.
