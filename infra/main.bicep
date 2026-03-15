targetScope = 'resourceGroup'

@description('Azure region for all resources')
param location string
param cosmosAccountName string
param storageAccountName string
param functionAppName string
param swaName string
param keyVaultName string

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: { location: location, keyVaultName: keyVaultName }
}

module cosmos 'modules/cosmos.bicep' = {
  name: 'cosmos'
  params: { location: location, accountName: cosmosAccountName }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: { location: location, storageAccountName: storageAccountName }
}

module functions 'modules/functions.bicep' = {
  name: 'functions'
  params: {
    location: location
    functionAppName: functionAppName
    storageAccountName: storageAccountName
    storageAccountId: storage.outputs.storageAccountId
    cosmosAccountEndpoint: cosmos.outputs.endpoint
    cosmosAccountId: cosmos.outputs.accountId
    cosmosAccountName: cosmosAccountName
    keyVaultName: keyVaultName
    keyVaultId: keyVault.outputs.keyVaultId
  }
}

module swa 'modules/swa.bicep' = {
  name: 'swa'
  params: { location: location, swaName: swaName }
}
