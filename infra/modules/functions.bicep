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

var appInsightsName = '${functionAppName}-insights'

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: { Application_Type: 'web' }
}

resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${functionAppName}-plan'
  location: location
  sku: { name: 'Y1', tier: 'Dynamic' }
  properties: { reserved: true }
}

resource storageAccountRef 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageQueueDataContributorRoleId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageTableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'NODE|22'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      cors: {
        allowedOrigins: ['https://lfm.dinosauruskeksi.com']
        supportCredentials: true
      }
      appSettings: [
        { name: 'AzureWebJobsStorage__accountName', value: storageAccountName }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'node' }
        { name: 'WEBSITE_NODE_DEFAULT_VERSION', value: '~22' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'COSMOS_ENDPOINT', value: cosmosAccountEndpoint }
        { name: 'BLOB_STORAGE_URL', value: 'https://${storageAccountName}.blob.${environment().suffixes.storage}' }
        { name: 'APP_BASE_URL', value: 'https://lfm.dinosauruskeksi.com' }
        { name: 'COOKIE_DOMAIN', value: '.dinosauruskeksi.com' }
        { name: 'BATTLE_NET_REGION', value: 'eu' }
        { name: 'BATTLE_NET_COOKIE_SECURE', value: 'true' }
        { name: 'LFM_CLIENT_ID', value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=battlenet-client-id)' }
        { name: 'LFM_CLIENT_SECRET', value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=battlenet-client-secret)' }
        { name: 'BATTLE_NET_REDIRECT_URI', value: 'https://lfm-api.dinosauruskeksi.com/api/battlenet/callback' }
        { name: 'HMAC_SECRET', value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=hmac-secret)' }
        { name: 'TOKEN_ENCRYPTION_KEY', value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=token-encryption-key)' }
      ]
    }
  }
}

// Grant Function App's MI read access to Key Vault secrets
resource keyVaultRef 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
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
@description('Cosmos DB account name (extracted from resource ID)')
param cosmosAccountName string

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

// Custom domain: managed cert + SNI binding (requires CNAME to resolve first)
resource managedCert 'Microsoft.Web/certificates@2023-12-01' = {
  name: 'lfm-api.dinosauruskeksi.com'
  location: location
  properties: {
    canonicalName: 'lfm-api.dinosauruskeksi.com'
    serverFarmId: hostingPlan.id
  }
}

resource customHostnameBinding 'Microsoft.Web/sites/hostNameBindings@2023-12-01' = {
  parent: functionApp
  name: 'lfm-api.dinosauruskeksi.com'
  properties: {
    sslState: 'SniEnabled'
    thumbprint: managedCert.properties.thumbprint
  }
}

output functionAppHostname string = functionApp.properties.defaultHostName
output functionAppPrincipalId string = functionApp.identity.principalId
