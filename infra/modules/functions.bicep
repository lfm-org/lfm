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
        allowedOrigins: ['https://raidcal.dinosauruskeksi.com']
        supportCredentials: true
      }
      appSettings: [
        { name: 'AzureWebJobsStorage', value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${storageAccountRef.listKeys().keys[0].value}' }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'node' }
        { name: 'WEBSITE_NODE_DEFAULT_VERSION', value: '~22' }
        { name: 'APPINSIGHTS_INSTRUMENTATIONKEY', value: appInsights.properties.InstrumentationKey }
        { name: 'COSMOS_ENDPOINT', value: cosmosAccountEndpoint }
        { name: 'BLOB_STORAGE_URL', value: 'https://${storageAccountName}.blob.${environment().suffixes.storage}' }
        { name: 'APP_BASE_URL', value: 'https://raidcal.dinosauruskeksi.com' }
        { name: 'COOKIE_DOMAIN', value: '.dinosauruskeksi.com' }
        { name: 'BATTLE_NET_REGION', value: 'eu' }
        { name: 'SISU_RAIDCAL_CLIENT_ID', value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=battlenet-client-id)' }
        { name: 'SISU_RAIDCAL_CLIENT_SECRET', value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=battlenet-client-secret)' }
        { name: 'BATTLE_NET_REDIRECT_URI', value: 'https://raidcal-api.dinosauruskeksi.com/api/battlenet/callback' }
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

output functionAppHostname string = functionApp.properties.defaultHostName
output functionAppPrincipalId string = functionApp.identity.principalId
