# Lfm API

Azure Functions .NET 10 isolated-worker backend for the Lfm raid-signup
application. See the repository root `README.md` for architecture,
development setup, and AI-usage acknowledgement.

## Contract

The generated OpenAPI 3.1 snapshot for this API is checked in at
[`openapi.yaml`](openapi.yaml). Regenerate it from the repo root with
`dotnet run --project tools/Lfm.OpenApiGenerator`; do not edit the
snapshot by hand. Downstream AGPL operators and the first-party SPA both
read from the static file, but deployment hostnames are intentionally not
published in the committed snapshot. The deploy build regenerates
`./publish/api/openapi.yaml` with `servers[0].url` set from
`API_HOSTNAME` before packaging the released API artifact.

`api/openapi.yaml` is validated on every pull request by
`OpenApiContractTests` in
[`tests/Lfm.Api.Tests/Openapi/`](../tests/Lfm.Api.Tests/Openapi/),
which runs inside the existing `verify` CI gate and compares the checked-in
snapshot to generator output.
