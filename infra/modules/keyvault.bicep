@description('Azure region')
param location string

@description('Key Vault name')
param keyVaultName string

@description('Log Analytics workspace resource ID for diagnostic settings')
param logAnalyticsWorkspaceId string

@description('Resource tags')
param tags object

resource keyVault 'Microsoft.KeyVault/vaults@2026-02-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    enablePurgeProtection: true
    // softDeleteRetentionInDays and enablePurgeProtection are immutable once set.
    // lfm-kv-prot was created with purge protection on and 90-day retention (F7 resolved).
    softDeleteRetentionInDays: 90
    networkAcls: {
      // defaultAction must be Allow on Consumption plan (free tier).
      // The AzureServices bypass only covers the Functions runtime storage connections,
      // not application-level Key Vault reference resolution by the platform.
      // See: https://learn.microsoft.com/en-us/azure/key-vault/general/overview-vnet-service-endpoints
      defaultAction: 'Allow'
      bypass: 'AzureServices'
      ipRules: []
      virtualNetworkRules: []
    }
  }
}

resource kvDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'kv-diagnostics'
  scope: keyVault
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      { categoryGroup: 'audit', enabled: true }
    ]
  }
}

output keyVaultId string = keyVault.id
output keyVaultUri string = keyVault.properties.vaultUri
