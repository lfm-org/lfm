// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

@description('Azure region')
param location string

@description('Function App name')
param functionAppName string

@description('Storage account name')
param storageAccountName string

@description('Cosmos DB account endpoint')
param cosmosAccountEndpoint string

@description('Key Vault name')
param keyVaultName string

@description('Cosmos DB account name')
param cosmosAccountName string

@description('Cosmos DB database name')
param cosmosDatabase string

@description('Frontend origin URL for CORS (no trailing slash)')
param frontendOrigin string

@description('Battle.net OAuth redirect URI')
param battleNetRedirectUri string

@description('Battle.net region code')
param battleNetRegion string

@description('Log Analytics workspace resource ID for diagnostic settings')
param logAnalyticsWorkspaceId string

@description('Privacy contact email address')
param privacyEmail string

@description('Data Protection KV key URI (versionless) for wrapping the key ring')
param dataProtectionKeyUri string

@description('Data Protection blob URI where the key ring XML is persisted')
param dataProtectionBlobUri string

@description('Resource tags')
param tags object

var appInsightsName = '${functionAppName}-insights'

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    DisableLocalAuth: true
    WorkspaceResourceId: logAnalyticsWorkspaceId
  }
}

resource hostingPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${functionAppName}-plan'
  location: location
  tags: tags
  kind: 'functionapp'
  sku: { name: 'Y1', tier: 'Dynamic' }
  properties: {}
}

resource storageAccountRef 'Microsoft.Storage/storageAccounts@2024-01-01' existing = {
  name: storageAccountName
}

var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageQueueDataContributorRoleId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageTableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      http20Enabled: true
      minTlsVersion: '1.2'
      netFrameworkVersion: 'v10.0'
      healthCheckPath: '/api/health'
      cors: {
        allowedOrigins: [frontendOrigin]
        supportCredentials: true
      }
      appSettings: [
        // Azure Functions runtime
        { name: 'AzureWebJobsStorage__accountName', value: storageAccountName }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING', value: 'Authorization=AAD' }
        // CosmosOptions (section: Cosmos)
        { name: 'Cosmos__Endpoint', value: cosmosAccountEndpoint }
        { name: 'Cosmos__DatabaseName', value: cosmosDatabase }
        // BlizzardOptions (section: Blizzard)
        { name: 'Blizzard__ClientId', value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=battlenet-client-id)' }
        { name: 'Blizzard__ClientSecret', value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=battlenet-client-secret)' }
        { name: 'Blizzard__Region', value: battleNetRegion }
        { name: 'Blizzard__RedirectUri', value: battleNetRedirectUri }
        { name: 'Blizzard__AppBaseUrl', value: frontendOrigin }
        // AuthOptions (section: Auth)
        { name: 'Auth__KeyVaultUrl', value: 'https://${keyVaultName}${environment().suffixes.keyvaultDns}/' }
        { name: 'Auth__DataProtectionKeyUri', value: dataProtectionKeyUri }
        // CorsOptions (section: Cors)
        { name: 'Cors__AllowedOrigins__0', value: frontendOrigin }
        // StorageOptions (section: Storage)
        { name: 'Storage__DataProtectionBlobUri', value: dataProtectionBlobUri }
        // Drives BlobReferenceClient (api/Program.cs) for the Phase 1
        // blob-backed reference reads (`/api/wow/reference/instances`,
        // `/api/wow/reference/specializations`). Managed identity flows through
        // DefaultAzureCredential — no shared key in app settings. Container
        // name defaults to "wow" in StorageOptions.
        { name: 'Storage__BlobServiceUri', value: storageAccountRef.properties.primaryEndpoints.blob }
        // Site — privacy contact email, bound to PrivacyContactOptions.Email
        // in the Functions app via the Options pattern (config key
        // `PrivacyContact:Email`; env mapping uses `__` as separator).
        { name: 'PrivacyContact__Email', value: privacyEmail }
        // AuditOptions (section: Audit). Without this, IActorHasher falls
        // back to IdentityActorHasher and emits plaintext battleNetId
        // (PII) into Application Insights. The "audit-hash-salt" secret
        // must exist in Key Vault before deploy — see
        // docs/threat-models/audit-log-pii-pipeline.md (backlog) and
        // api/Options/AuditOptions.cs.
        { name: 'Audit__HashSalt', value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=audit-hash-salt)' }
      ]
    }
  }
}

// Grant Function App's MI read access to Key Vault secrets
resource keyVaultRef 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVaultRef.id, functionApp.id, keyVaultSecretsUserRoleId)
  scope: keyVaultRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Grant Function App's MI Cosmos DB data contributor role
resource cosmosAccountRef 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' existing = {
  name: cosmosAccountName
}

var cosmosDataContributorRoleId = '00000000-0000-0000-0000-000000000002'
resource cosmosRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = {
  name: guid(cosmosAccountRef.id, functionApp.id, cosmosDataContributorRoleId)
  parent: cosmosAccountRef
  properties: {
    roleDefinitionId: '${cosmosAccountRef.id}/sqlRoleDefinitions/${cosmosDataContributorRoleId}'
    principalId: functionApp.identity.principalId
    scope: cosmosAccountRef.id
  }
}

// Grant Function App's MI storage roles (required for AzureWebJobsStorage__accountName pattern)
resource storageBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountRef.id, functionApp.id, storageBlobDataOwnerRoleId)
  scope: storageAccountRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource storageQueueRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountRef.id, functionApp.id, storageQueueDataContributorRoleId)
  scope: storageAccountRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributorRoleId)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource storageTableRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountRef.id, functionApp.id, storageTableDataContributorRoleId)
  scope: storageAccountRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributorRoleId)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Grant Function App's MI the Monitoring Metrics Publisher role on App Insights.
// Required because DisableLocalAuth is true — without this, telemetry silently drops.
var monitoringMetricsPublisherRoleId = '3913510d-42f4-4e42-8a64-420c390055eb'
resource appInsightsRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appInsights.id, functionApp.id, monitoringMetricsPublisherRoleId)
  scope: appInsights
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions', monitoringMetricsPublisherRoleId)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource ftpCredentials 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: functionApp
  name: 'ftp'
  properties: { allow: false }
}

resource scmCredentials 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: functionApp
  name: 'scm'
  properties: { allow: false }
}

resource functionDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'function-diagnostics'
  scope: functionApp
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      { category: 'FunctionAppLogs', enabled: true }
    ]
  }
}

output functionAppHostname string = functionApp.properties.defaultHostName
output functionAppPrincipalId string = functionApp.identity.principalId
