targetScope = 'resourceGroup'

@description('Azure region for all resources')
param location string
param cosmosAccountName string
param storageAccountName string
param functionAppName string
param swaName string
param keyVaultName string

@description('Object ID of the CI/CD service principal (for Cosmos data-plane access during migrations)')
param ciPrincipalId string = ''

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: { location: location, keyVaultName: keyVaultName }
}

module cosmos 'modules/cosmos.bicep' = {
  name: 'cosmos'
  params: { location: location, accountName: cosmosAccountName, ciPrincipalId: ciPrincipalId }
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
    cosmosAccountEndpoint: cosmos.outputs.endpoint
    cosmosAccountName: cosmosAccountName
    keyVaultName: keyVaultName
  }
}

module swa 'modules/swa.bicep' = {
  name: 'swa'
  params: { location: location, swaName: swaName }
}
