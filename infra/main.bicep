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

@description('Cosmos DB database name')
param cosmosDatabase string

@description('Frontend origin URL (no trailing slash)')
param frontendOrigin string

@description('Cookie domain')
param cookieDomain string

@description('Battle.net OAuth redirect URI')
param battleNetRedirectUri string

@description('Battle.net region code')
param battleNetRegion string

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
    cosmosDatabase: cosmosDatabase
    frontendOrigin: frontendOrigin
    cookieDomain: cookieDomain
    battleNetRedirectUri: battleNetRedirectUri
    battleNetRegion: battleNetRegion
    keyVaultName: keyVaultName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    privacyEmail: privacyEmail
    tags: tags
  }
}

module swa 'modules/swa.bicep' = {
  name: '${uniqueString(resourceGroup().id, location)}-swa'
  params: {
    location: location
    swaName: swaName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    tags: tags
  }
}
