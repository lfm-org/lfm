# Base images pinned to sha256 digests (Phase 1.2 of devsecops remediation).
# Tags kept as the human-readable reference. Dependabot docker ecosystem refreshes weekly.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:c0790639332692a0d56cdd81ed581cfd24d040d9839764c138994866df89a3b6 AS build
WORKDIR /src

COPY shared/Lfm.Contracts/Lfm.Contracts.csproj shared/Lfm.Contracts/
COPY api/Lfm.Api.csproj api/
RUN dotnet restore api/Lfm.Api.csproj

COPY shared/ shared/
COPY api/ api/
ARG E2ETest=false
RUN dotnet publish api/Lfm.Api.csproj -c Release -p:E2ETest=$E2ETest -o /app/publish

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0@sha256:a72bc040278f00001210db769d602f4aa515843e006b5c81f39156b1f7286635
ENV AzureWebJobsScriptRoot=/home/site/wwwroot
COPY --from=build /app/publish /home/site/wwwroot
# Runs as the base image's default user. Adding `USER app` with the .NET 8+ non-root
# pattern requires `chown -R app:app /home` and port 80 rebinding — deferred
# because the Azure Functions prebuilt runtime handles user privileges internally
# and the MCR carve-out downgrades docker.HC-1 from block to warn. See SECURITY.md
# Known Limitations.
