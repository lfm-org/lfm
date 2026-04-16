# Base images pinned to sha256 digests (Phase 1.2 of devsecops remediation).
# Tags kept as the human-readable reference. Dependabot docker ecosystem refreshes weekly.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:adc02be8b87957d07208a4a3e51775935b33bad3317de8c45b1e67357b4c073b AS build
WORKDIR /src

COPY shared/Lfm.Contracts/Lfm.Contracts.csproj shared/Lfm.Contracts/
COPY api/Lfm.Api.csproj api/
RUN dotnet restore api/Lfm.Api.csproj

COPY shared/ shared/
COPY api/ api/
RUN dotnet publish api/Lfm.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0@sha256:cb2a536e3a5b54b12081f95a116e0a4f70d84a2b276181c83583acb23fef4141
ENV AzureWebJobsScriptRoot=/home/site/wwwroot
COPY --from=build /app/publish /home/site/wwwroot
# Runs as the base image's default user. Adding `USER app` with the .NET 8+ non-root
# pattern requires `chown -R app:app /home` and port 80 rebinding — deferred
# because the Azure Functions prebuilt runtime handles user privileges internally
# and the MCR carve-out downgrades docker.HC-1 from block to warn. See SECURITY.md
# Known Limitations.
