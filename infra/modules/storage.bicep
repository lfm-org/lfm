// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

@description('Azure region')
param location string

@description('Storage account name')
param storageAccountName string

@description('Log Analytics workspace resource ID for diagnostic settings')
param logAnalyticsWorkspaceId string

@description('Resource tags')
param tags object

resource storageAccount 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    // defaultAction is Allow because the Function App (Consumption plan) reads
    // blobs directly and cannot use the AzureServices bypass for data-plane
    // access without VNet integration. allowBlobPublicAccess: false still
    // prevents any container from ever being made public.
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
      ipRules: []
      virtualNetworkRules: []
    }
  }
}

resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    deleteRetentionPolicy: { enabled: true, days: 7 }
    containerDeleteRetentionPolicy: { enabled: true, days: 7 }
  }
}

resource wowContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: blobServices
  name: 'wow'
  properties: {
    publicAccess: 'None'
  }
}

resource dataProtectionContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: blobServices
  name: 'dataprotection'
  properties: {
    publicAccess: 'None'
  }
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: blobServices
  name: 'deployments'
  properties: {
    publicAccess: 'None'
  }
}

resource blobDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'blob-diagnostics'
  scope: blobServices
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      { category: 'StorageRead', enabled: true }
      { category: 'StorageWrite', enabled: true }
      { category: 'StorageDelete', enabled: true }
    ]
  }
}

resource storageLock 'Microsoft.Authorization/locks@2020-05-01' = {
  name: '${storageAccountName}-lock'
  scope: storageAccount
  properties: {
    level: 'CanNotDelete'
    notes: 'Prevent accidental deletion of storage account (Functions runtime + wow blob data).'
  }
}

output storageAccountId string = storageAccount.id
output blobEndpoint string = storageAccount.properties.primaryEndpoints.blob
output dataProtectionBlobUri string = '${storageAccount.properties.primaryEndpoints.blob}dataprotection/keys.xml'
output deploymentContainerName string = deploymentContainer.name
