targetScope = 'resourceGroup'

@description('Azure region for all resources')
param location string
param cosmosAccountName string
param storageAccountName string
param functionAppName string
param swaName string
param keyVaultName string
param logAnalyticsWorkspaceName string

@description('Privacy contact email address')
param privacyEmail string

@description('Tags applied to all resources')
param tags object

module logAnalytics 'modules/loganalytics.bicep' = {
  name: '${uniqueString(resourceGroup().id, location)}-loganalytics'
  params: { location: location, workspaceName: logAnalyticsWorkspaceName, tags: tags }
}

module keyVault 'modules/keyvault.bicep' = {
  name: '${uniqueString(resourceGroup().id, location)}-keyvault'
  params: {
    location: location
    keyVaultName: keyVaultName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    tags: tags
  }
}

module cosmos 'modules/cosmos.bicep' = {
  name: '${uniqueString(resourceGroup().id, location)}-cosmos'
  params: {
    location: location
    accountName: cosmosAccountName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    tags: tags
  }
}

module storage 'modules/storage.bicep' = {
  name: '${uniqueString(resourceGroup().id, location)}-storage'
  params: {
    location: location
    storageAccountName: storageAccountName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    tags: tags
  }
}

module functions 'modules/functions.bicep' = {
  name: '${uniqueString(resourceGroup().id, location)}-functions'
  dependsOn: [storage, keyVault]
  params: {
    location: location
    functionAppName: functionAppName
    storageAccountName: storageAccountName
    cosmosAccountEndpoint: cosmos.outputs.endpoint
    cosmosAccountName: cosmosAccountName
    keyVaultName: keyVaultName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    privacyEmail: privacyEmail
    tags: tags
  }
}

module swa 'modules/swa.bicep' = {
  name: '${uniqueString(resourceGroup().id, location)}-swa'
  params: { location: location, swaName: swaName, tags: tags }
}
