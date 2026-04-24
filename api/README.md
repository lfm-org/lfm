# Lfm API

Azure Functions .NET 10 isolated-worker backend for the Lfm raid-signup
application. See the repository root `README.md` for architecture,
development setup, and AI-usage acknowledgement.

## Contract

The source-of-truth OpenAPI 3.1 contract for this API is checked in at
[`openapi.yaml`](openapi.yaml). Downstream AGPL operators and the
first-party SPA both read from the static file — the API does **not**
serve a live schema in production. See the root `README.md` section on
the API contract for the full hybrid (generator-in-dev, static-in-prod)
rationale.

`api/openapi.yaml` is validated on every pull request by
`OpenApiContractTests` in
[`tests/Lfm.Api.Tests/Openapi/`](../tests/Lfm.Api.Tests/Openapi/),
which runs inside the existing `verify` CI gate.
