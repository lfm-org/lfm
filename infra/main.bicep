// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

targetScope = 'resourceGroup'

@description('Azure region for all resources')
param location string

@description('Cosmos DB account name')
@minLength(3)
@maxLength(44)
param cosmosAccountName string

@description('Storage account name (lowercase alphanumeric only)')
@minLength(3)
@maxLength(24)
param storageAccountName string

@description('Function App name')
@minLength(2)
@maxLength(60)
param functionAppName string

@description('Static Web App name')
@minLength(1)
@maxLength(40)
param swaName string

@description('Key Vault name')
@minLength(3)
@maxLength(24)
param keyVaultName string

@description('Log Analytics workspace name')
@minLength(4)
@maxLength(63)
param logAnalyticsWorkspaceName string

@description('Privacy contact email address')
param privacyEmail string

@description('Cosmos DB database name')
param cosmosDatabase string

@description('Frontend origin URL (no trailing slash)')
param frontendOrigin string

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
    databaseName: cosmosDatabase
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
  params: {
    location: location
    functionAppName: functionAppName
    storageAccountName: storageAccountName
    cosmosAccountEndpoint: cosmos.outputs.endpoint
    cosmosAccountName: cosmosAccountName
    cosmosDatabase: cosmosDatabase
    frontendOrigin: frontendOrigin
    battleNetRedirectUri: battleNetRedirectUri
    battleNetRegion: battleNetRegion
    keyVaultName: keyVaultName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    privacyEmail: privacyEmail
    dataProtectionKeyUri: keyVault.outputs.dataProtectionKeyUri
    dataProtectionBlobUri: storage.outputs.dataProtectionBlobUri
    tags: tags
  }
}

module dataProtection 'modules/dataprotection.bicep' = {
  name: '${uniqueString(resourceGroup().id, location)}-dataprotection'
  params: {
    keyVaultName: keyVaultName
    storageAccountName: storageAccountName
    functionsManagedIdentityPrincipalId: functions.outputs.functionAppPrincipalId
  }
}

// Alerts are deployed separately (post-Bicep step in deploy-infra.yml)
// because Http5xx/HttpResponseTime metrics only register after the
// Function App has code deployed and has served HTTP traffic.

module swa 'modules/swa.bicep' = {
  name: '${uniqueString(resourceGroup().id, location)}-swa'
  params: {
    location: location
    swaName: swaName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    tags: tags
  }
}
