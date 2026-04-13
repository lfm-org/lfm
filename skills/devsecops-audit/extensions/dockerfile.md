# Extension — Dockerfile and docker-compose

**Applies to:** `**/Dockerfile`, `**/Dockerfile.*`, `**/docker-compose*.yml`, `**/docker-compose*.yaml`.

**Detection signals:**

- File name matches `Dockerfile` (exact) or `Dockerfile.*` (e.g. `Dockerfile.test`).
- File name matches `docker-compose*.y?ml` (including `docker-compose.test.yml`, `docker-compose.override.yml`).

**Applies to rubric sections:** §4 (CICD-SEC-3, -9), §5.1 items 8, 13, 19, §5.3 item 1.

**Note:** the lfm repo uses Azure Functions Flex Consumption for production (no production Dockerfile), but `docker-compose.test.yml` pulls images for the E2E test stack. Those pulls are subject to the same pin-by-digest smell as production images.

## Smell codes

### `docker.HC-1` — `USER root` or no `USER` directive

**Pattern:** a Dockerfile that explicitly sets `USER root` (or an equivalent UID of 0) or that has no `USER` directive at all.

**Detection (ripgrep):**
```
USER\s+(root|0)\s*$
```
Plus: any Dockerfile with zero `USER` lines.

**Severity:** `block`

**Rubric:** devsecops.md §5.1.13; OWASP Docker Security Cheat Sheet.

**Remediation action:**
> Declare a non-root `USER` explicitly. Create a dedicated user in the Dockerfile: `RUN adduser --system --uid 10001 app && USER app`. For language base images, many already ship a `nonroot` or numeric UID; prefer those.

### `docker.HC-2` — `FROM image:tag` without `@sha256:` digest

**Pattern:** any `FROM` line whose reference is `image:tag` or `image:tag@sha256:` but the sha256 portion is missing.

**Detection (two-pass, default ripgrep — avoids lookbehind):**

1. Enumerate every `FROM` line: `rg -nE '^FROM\s+\S+' **/Dockerfile* **/Dockerfile`
2. For each match, check whether the image reference contains `@sha256:[a-f0-9]{64}`. Any reference without a digest → `docker.HC-2` finding. All references digest-pinned across the file → `docker.POS-1` positive.

**Compose variant (portable regex — `\n` in character classes is grep-dialect dependent, so use `[:space:]`):**

- Unpinned: `rg -nE '^\s*image:\s*[^@[:space:]]+:[^@[:space:]]+$' docker-compose*.y?ml`
- Pinned: `rg -nE '^\s*image:\s*[^[:space:]]+@sha256:[a-f0-9]{64}\b' docker-compose*.y?ml`

(If the caller insists on a single-pass regex, `rg --pcre2 '^FROM\s+[^\s]+(?<!@sha256:[a-f0-9]{64})\s*($|AS)'` works with PCRE2.)

**Severity:** `block` (third-party registry) / `warn` (organization-internal registry with signed tags, or Microsoft-owned `mcr.microsoft.com` registries per carve-out)

**Rubric:** devsecops.md §5.1.8; CICD-SEC-3; CICD-SEC-9.

**Remediation action:**
> Pin by digest: `FROM python:3.12-slim@sha256:<digest>`. Use `docker pull` + `docker image inspect` to capture the current digest. Update via Dependabot or Renovate with digest-pinning enabled.

**Compose variant:** the same rule applies to `image:` keys in `docker-compose*.yml`. Example:
```yaml
services:
  cosmos:
    image: mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest  # SMELL
```
Must become:
```yaml
    image: mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator@sha256:<digest>
```

### `docker.HC-3` — Build args containing secret material

**Pattern:** `ARG` declarations whose name matches `(password|secret|token|key|credential)` (case-insensitive), used via `--build-arg` at build time. Build args are baked into layer metadata and visible via `docker history`.

**Detection (ripgrep):**
```
ARG\s+[A-Z_]*(?:PASSWORD|SECRET|TOKEN|KEY|CREDENTIAL)[A-Z_]*
```

**Severity:** `block`

**Rubric:** devsecops.md §5.1.1; CICD-SEC-6.

**Remediation action:**
> Remove the secret-shaped ARG. Inject the secret via BuildKit `--mount=type=secret` (never baked into a layer) or at runtime via environment variables from a vault.

### `docker.HC-4` — Writable root filesystem in compose

**Pattern:** a service in `docker-compose*.yml` without `read_only: true`, especially for services that don't need to write to the root filesystem (cosmos emulator, azurite, functions host).

**Detection:** parse `services:` in each compose file and list services lacking `read_only: true`.

**Severity:** `warn`

**Rubric:** devsecops.md §5.2.5 (K8s analog); OWASP Docker Security Cheat Sheet.

**Remediation action:**
> Add `read_only: true` to each service. Mount any writable paths (temp dirs, data dirs) as explicit `tmpfs:` or named volumes.

### `docker.HC-5` — Privileged container

**Pattern:** `privileged: true` in a compose service or a `docker run --privileged` in any script committed to the repo.

**Detection (ripgrep):**
```
privileged\s*:\s*true
--privileged
```

**Severity:** `block`

**Rubric:** OWASP Docker Security Cheat Sheet; §5.2.5.

**Remediation action:**
> Remove `privileged: true`. If the container genuinely needs a capability (e.g. `NET_ADMIN`), use `cap_add:` with only that specific capability.

## Positive signals

### `docker.POS-1` — All `FROM` references pinned to `@sha256:`

**Pattern:** every `FROM` line and every compose `image:` key uses a `@sha256:<64-hex>` suffix.

### `docker.POS-2` — Non-root `USER` declared

**Pattern:** every Dockerfile ends with a `USER` directive whose value is not `root` or `0`.

## Carve-outs

- **Do not flag `docker.HC-2` on test-only compose files that pull from Microsoft-owned registries** (`mcr.microsoft.com`) as `block`; downgrade to `warn`. Microsoft's registry ships signed tags and the trust model is different from arbitrary Docker Hub images. Still a smell, but not a blocker for test fixtures.
