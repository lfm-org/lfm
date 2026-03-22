# OWASP Security Review: Azure Resource Group `lfm`

- Date: 2026-03-22
- Scope: Azure resource group `lfm` in `westeurope`
- Method: Read-only Azure CLI inspection of deployed resource configuration
- Constraint: No secret values were queried or retrieved; findings are based on configuration state and non-sensitive metadata only

## Resources Reviewed

- Azure Static Web App
- Azure Function App and hosting plan
- Azure Cosmos DB account
- Azure Storage account
- Azure Key Vault
- Application Insights

## Executive Summary

The highest-risk issues are public data exposure and overly broad network reachability. The storage account permits public blob access and contains at least one anonymously readable container. Storage, Cosmos DB, Key Vault, and the Function App are also exposed over public network paths with minimal restriction. Logging and secret lifecycle controls are present but not hardened.

## Azure Free-Tier Context

This review should not be read as "free tier equals insecure." The current deployment mixes one actual free SKU, one legacy serverless hosting plan, and several services where the main security gaps are configuration choices rather than unavoidable free-tier limitations.

- Static Web Apps:
  - The frontend runs on the Static Web Apps Free plan.
  - Microsoft documents that the Free plan does not include private endpoints or allowed IP range restrictions.
  - Commentary: if you want to harden the frontend edge itself with private ingress or IP allowlists, that specific control requires at least a higher Static Web Apps plan. This is a genuine plan limitation, but it is not the source of the highest-risk findings in this review.
- Function App:
  - The backend runs on the Azure Functions Consumption plan (`Y1`), which Microsoft now treats as a legacy hosting option and recommends replacing with Flex Consumption for new serverless apps.
  - Microsoft documents App Service private endpoints for Basic, Standard, Premium, Isolated, and Functions Premium plans.
  - Commentary: if the target state is private-endpoint-only ingress for the Function App, that likely requires a hosting-plan change. However, the current lack of App Service auth, the allow-all access restrictions, and enabled publishing credentials are not free-tier constraints; those are hardening gaps in the present configuration.
- Cosmos DB:
  - The deployed Cosmos DB account is not using the lifetime free tier (`enableFreeTier: false`).
  - Commentary: the Cosmos findings are therefore not explained by free-tier limits. The public-network exposure and key-based auth posture should be treated as ordinary security misconfiguration. If cost reduction is a goal, free tier is something that must be chosen at account creation, but it does not change the recommendation to restrict network access and prefer managed identity patterns.
- Storage:
  - The Storage account findings are not free-tier artifacts.
  - Commentary: Microsoft explicitly recommends disabling anonymous blob access for storage accounts unless the scenario truly requires public content, and even then recommends isolating public content into a separate account. The public container found here is a security decision, not an unavoidable consequence of low-cost Azure usage.
- Key Vault:
  - The Key Vault findings are also not free-tier tradeoffs.
  - Commentary: Microsoft guidance recommends disabling public network access and using private endpoints where possible, enabling audit logging, and enabling purge protection. The current public exposure, missing diagnostics, 7-day retention, and absent purge protection are weaker than Microsoft's recommended posture, independent of pricing tier.
- Logging and monitoring:
  - This is the one area where cost pressure often explains weaker posture.
  - Microsoft notes that standard metrics and activity logs are free, while Azure Monitor log ingestion, retention, export, and platform-log streaming are billed.
  - Commentary: the absence of diagnostic settings is understandable in a cost-sensitive environment, but it remains a real OWASP monitoring gap. If the budget is strict, prioritize audit coverage on Key Vault and the Function App first, then add Storage and Cosmos DB.

### Cost-Sensitive Interpretation

- No-cost or low-cost hardening:
  - Remove anonymous blob access.
  - Disable FTP and SCM publishing credentials.
  - Enable App Service auth if the API is meant to be gated there.
  - Tighten Function App access restrictions where feasible.
  - Increase Key Vault secret hygiene with expiration and rotation policy.
- Likely plan or ongoing-cost decisions:
  - Upgrading Static Web Apps if frontend private ingress or IP restrictions are required.
  - Moving the Function App off the current Consumption plan if private endpoint isolation is required.
  - Enabling richer diagnostic logging destinations that incur Azure Monitor or storage costs.
- Bottom line:
  - Only a subset of the stronger network-isolation controls are constrained by low-cost Azure plans.
  - The most serious findings in this review, especially anonymous blob access and broad public exposure with permissive management paths, are still avoidable and should not be dismissed as "free-tier issues."

## Findings

### 1. Anonymous blob access is enabled

- Severity: Critical
- OWASP: A01 Broken Access Control, A05 Security Misconfiguration
- Evidence:
  - The storage account allows blob public access.
  - At least one blob container is configured with anonymous read access.
  - The storage firewall default action is allow.
- Risk:
  - Files in the exposed container can be fetched without authentication.
  - Misplaced application data, exports, or generated content can become internet-readable immediately.

### 2. Core data services are publicly reachable

- Severity: High
- OWASP: A05 Security Misconfiguration
- Evidence:
  - Storage has no IP or virtual network restrictions and defaults to allow.
  - Cosmos DB has public network access enabled, no IP rules, and no virtual network filter.
  - Key Vault has public network access enabled and no network ACLs configured.
- Risk:
  - The attack surface is broader than necessary.
  - If an application credential, token, or identity path is abused, the services are reachable directly from public networks.

### 3. Function App ingress and publishing access are too open

- Severity: High
- OWASP: A01 Broken Access Control, A05 Security Misconfiguration, A07 Identification and Authentication Failures
- Evidence:
  - App Service authentication is disabled.
  - Main site and SCM access restrictions allow all traffic.
  - Basic publishing credentials remain allowed for both SCM and FTP endpoints.
- Risk:
  - Management and deployment paths are exposed more broadly than required.
  - This increases the blast radius of credential compromise and weakens defense in depth.

### 4. Cosmos DB still allows key-based authentication

- Severity: Medium
- OWASP: A05 Security Misconfiguration, A07 Identification and Authentication Failures
- Evidence:
  - Local authentication is not disabled.
  - Key-based metadata writes are not disabled.
- Risk:
  - Long-lived keys remain a valid access path even if the preferred model is managed identity.
  - Operational drift can reintroduce shared-secret usage.

### 5. Diagnostic settings are not configured on key resources

- Severity: Medium
- OWASP: A09 Security Logging and Monitoring Failures
- Evidence:
  - No diagnostic settings were configured on the reviewed Storage, Cosmos DB, Key Vault, or Function App resources.
- Risk:
  - Audit coverage is limited for access events, configuration changes, and incident investigation.
  - Detection and response quality will be weaker during a real security event.

### 6. Secret lifecycle hardening is incomplete

- Severity: Medium
- OWASP: A02 Cryptographic Failures, A05 Security Misconfiguration
- Evidence:
  - Enabled secrets exist in Key Vault without expiration metadata.
  - Soft-delete retention is short.
  - Purge protection was not reported as enabled.
- Risk:
  - Rotation discipline is easier to miss.
  - Recovery and deletion protection are weaker than typical production hardening targets.

## Positive Controls

- The Function App uses a system-assigned managed identity.
- Key Vault uses RBAC authorization instead of legacy access policies.
- TLS 1.2 is enforced on the reviewed Function App, Storage, and Cosmos DB resources.
- The Function App identity appears narrowly scoped to required Storage data roles and Key Vault secret read access.

## Control Gaps Outside Direct Findings

- No Azure Policy assignments were active at the resource-group scope during the review.
- No Azure Advisor security recommendations were returned for the resource group at review time.

These are not proofs of security. They indicate an absence of preventive governance and native detections at this scope.

## Recommended Remediation Order

1. Remove anonymous access from the exposed blob container and disable account-level blob public access unless there is a documented business requirement.
2. Restrict Storage, Cosmos DB, and Key Vault with private endpoints or explicit network allowlists.
3. Disable FTP and SCM basic publishing access and restrict Function App and SCM ingress.
4. Disable Cosmos DB local auth and key-based metadata writes if the application can rely on managed identity only.
5. Enable diagnostic settings for Storage, Cosmos DB, Key Vault, and the Function App.
6. Add secret expiration and rotation policy, increase deletion safety, and enable purge protection on Key Vault.

## Reviewer Notes

This review was configuration-focused and performed from Azure control-plane data with read-only CLI access. It did not include application code review, runtime penetration testing, dependency analysis, or any retrieval of secret values.

## Official References

- Azure Static Web Apps quotas and plan limits: https://learn.microsoft.com/en-us/azure/static-web-apps/quotas
- Azure Functions hosting options: https://learn.microsoft.com/en-us/azure/azure-functions/functions-scale
- Azure App Service private endpoints: https://learn.microsoft.com/en-us/azure/app-service/overview-private-endpoint
- Azure App Service access restrictions: https://learn.microsoft.com/en-us/azure/app-service/overview-access-restrictions
- Azure Storage anonymous access remediation guidance: https://learn.microsoft.com/en-us/azure/storage/blobs/anonymous-read-access-overview
- Azure Cosmos DB security guidance: https://learn.microsoft.com/en-us/azure/cosmos-db/security
- Azure Key Vault security guidance: https://learn.microsoft.com/en-us/azure/key-vault/general/secure-key-vault
- Azure Key Vault soft-delete and purge protection: https://learn.microsoft.com/en-us/azure/key-vault/general/soft-delete-overview
- Azure Monitor pricing: https://azure.microsoft.com/en-in/pricing/details/monitor/
