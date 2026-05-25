# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

# Base image pinned to sha256 digest (matching repo Docker pin policy).
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:c0790639332692a0d56cdd81ed581cfd24d040d9839764c138994866df89a3b6

RUN dotnet tool install --tool-path /opt/cosmosdbshell CosmosDBShell --version 1.0.213-preview \
    && chmod -R a+rX /opt/cosmosdbshell

RUN groupadd --system --gid 10001 localinit \
    && useradd --system --uid 10001 --gid 10001 --home-dir /tmp/local-init-home --shell /usr/sbin/nologin localinit \
    && mkdir -p /home/local-secrets /tmp/local-init-home \
    && chown -R 10001:10001 /home/local-secrets /tmp/local-init-home

ENV PATH="/opt/cosmosdbshell:${PATH}"

WORKDIR /tmp

USER 10001:10001
