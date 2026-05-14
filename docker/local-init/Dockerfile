# Base image pinned to sha256 digest (matching repo Docker pin policy).
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:8a90a473da5205a16979de99d2fc20975e922c68304f5c79d564e666dc3982fc

RUN dotnet tool install --tool-path /opt/cosmosdbshell CosmosDBShell --version 1.0.213-preview

ENV PATH="/opt/cosmosdbshell:${PATH}"

WORKDIR /tmp
