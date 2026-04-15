# Base images pinned to sha256 digests (Phase 1.2 of devsecops remediation).
# Tags kept as the human-readable reference. Dependabot docker ecosystem refreshes weekly.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:caa1a2d363812eb21df9c56f01fa59d5d81bbb03103cb6a48f32dd7e80855616 AS build
WORKDIR /src

COPY shared/Lfm.Contracts/Lfm.Contracts.csproj shared/Lfm.Contracts/
COPY api/Lfm.Api.csproj api/
RUN dotnet restore api/Lfm.Api.csproj

COPY shared/ shared/
COPY api/ api/
RUN dotnet publish api/Lfm.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0@sha256:af634f8ded5c98434d5f832de457fcc5f42738439ac55510a4070e2aa1a24802
ENV AzureWebJobsScriptRoot=/home/site/wwwroot
COPY --from=build /app/publish /home/site/wwwroot
# Runs as the base image's default user. Adding `USER app` with the .NET 8+ non-root
# pattern requires `chown -R app:app /home` and port 80 rebinding — deferred
# because the Azure Functions prebuilt runtime handles user privileges internally
# and the MCR carve-out downgrades docker.HC-1 from block to warn. See SECURITY.md
# Known Limitations.
