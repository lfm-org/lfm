// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

@description('Key Vault name containing the Data Protection wrapping key')
param keyVaultName string

@description('Storage account name where the Data Protection key ring is persisted')
param storageAccountName string

@description('Principal ID of the Function App managed identity')
param functionsManagedIdentityPrincipalId string

// Key Vault Crypto User: wrapKey / unwrapKey on keys
var keyVaultCryptoUserRoleId = '12338af0-0e69-4776-bea7-57ae8d297424'

// Storage Blob Data Contributor: read/write blobs in the container
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource keyVaultRef 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

resource storageAccountRef 'Microsoft.Storage/storageAccounts@2024-01-01' existing = {
  name: storageAccountName
}

resource blobServicesRef 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' existing = {
  parent: storageAccountRef
  name: 'default'
}

resource dataProtectionContainerRef 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' existing = {
  parent: blobServicesRef
  name: 'dataprotection'
}

resource kvCryptoUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVaultRef.id, functionsManagedIdentityPrincipalId, keyVaultCryptoUserRoleId)
  scope: keyVaultRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCryptoUserRoleId)
    principalId: functionsManagedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource storageBlobContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(dataProtectionContainerRef.id, functionsManagedIdentityPrincipalId, storageBlobDataContributorRoleId)
  scope: dataProtectionContainerRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: functionsManagedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}
