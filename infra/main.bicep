targetScope = 'resourceGroup'

@description('Azure region for all resources')
param location string
param cosmosAccountName string
param storageAccountName string
param functionAppName string
param swaName string
param keyVaultName string
param logAnalyticsWorkspaceName string

module logAnalytics 'modules/loganalytics.bicep' = {
  name: 'loganalytics'
  params: { location: location, workspaceName: logAnalyticsWorkspaceName }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    location: location
    keyVaultName: keyVaultName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
  }
}

module cosmos 'modules/cosmos.bicep' = {
  name: 'cosmos'
  params: {
    location: location
    accountName: cosmosAccountName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    storageAccountName: storageAccountName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
  }
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
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
  }
}

module swa 'modules/swa.bicep' = {
  name: 'swa'
  params: { location: location, swaName: swaName }
}
