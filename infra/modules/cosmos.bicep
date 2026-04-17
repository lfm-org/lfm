// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

@description('Azure region')
param location string

@description('Cosmos DB account name')
param accountName string

@description('Log Analytics workspace resource ID for diagnostic settings')
param logAnalyticsWorkspaceId string

@description('Cosmos DB database name')
param databaseName string

@description('Resource tags')
param tags object

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: accountName
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    enableFreeTier: true
    locations: [{ locationName: location, failoverPriority: 0 }]
    consistencyPolicy: { defaultConsistencyLevel: 'Session' }
    capabilities: []
    minimalTlsVersion: 'Tls12'
    disableLocalAuth: true
    disableKeyBasedMetadataWriteAccess: true
    ipRules: [
      { ipAddressOrRange: '0.0.0.0' } // Accept connections from within Azure datacenters
    ]
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: { id: databaseName }
    // Free tier (enableFreeTier: true) covers the first 1,000 RU/s and 25 GB
    // account-wide. A second provisioned-throughput database would immediately
    // incur cost (~$58/month per 1,000 RU/s). Keep exactly one database here.
    options: { throughput: 1000 }
  }
}

resource raidersContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'raiders'
  properties: {
    resource: {
      id: 'raiders'
      partitionKey: { paths: ['/battleNetId'], kind: 'Hash' }
      // TTL=-1: enable TTL support but rely on per-document ttl field.
      // Each raider doc sets ttl = 180 days on every write; Cosmos auto-prunes inactive
      // accounts so orphaned profiles eventually fall off without manual intervention.
      // The raider-cleanup timer function still scrubs run data (90-day inactivity) before
      // the doc itself expires, preserving the GDPR purge flow.
      defaultTtl: -1
      indexingPolicy: {
        automatic: true
        indexingMode: 'consistent'
        // Only the cleanup query filters on lastSeenAt. All other access is
        // point-reads by partition key, which don't use secondary indexes.
        includedPaths: [
          { path: '/lastSeenAt/?' }
        ]
        excludedPaths: [
          { path: '/*' }
        ]
      }
    }
  }
}

resource runsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'runs'
  properties: {
    resource: {
      id: 'runs'
      partitionKey: { paths: ['/id'], kind: 'Hash' }
      // TTL=-1: enable TTL support but rely on per-document ttl field.
      // Each run document sets its own ttl = seconds until startTime + 7 days.
      defaultTtl: -1
      indexingPolicy: {
        automatic: true
        indexingMode: 'consistent'
        compositeIndexes: [
          [
            { path: '/visibility', order: 'ascending' }
            { path: '/creatorGuild', order: 'ascending' }
            { path: '/startTime', order: 'ascending' }
          ]
        ]
      }
    }
  }
}

resource guildsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'guilds'
  properties: {
    resource: {
      id: 'guilds'
      partitionKey: { paths: ['/id'], kind: 'Hash' }
      // Point-read only (by partition key). Exclude all secondary indexes to save write RU.
      indexingPolicy: {
        automatic: true
        indexingMode: 'consistent'
        includedPaths: []
        excludedPaths: [{ path: '/*' }]
      }
    }
  }
}

resource migrationsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'migrations'
  properties: {
    resource: {
      id: 'migrations'
      partitionKey: { paths: ['/id'], kind: 'Hash' }
      // Point-read only (by partition key). Exclude all secondary indexes to save write RU.
      indexingPolicy: {
        automatic: true
        indexingMode: 'consistent'
        includedPaths: []
        excludedPaths: [{ path: '/*' }]
      }
    }
  }
}

resource cosmosLock 'Microsoft.Authorization/locks@2020-05-01' = {
  name: '${accountName}-lock'
  scope: cosmosAccount
  properties: {
    level: 'CanNotDelete'
    notes: 'Prevent accidental deletion of Cosmos DB account and its data.'
  }
}

resource cosmosDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'cosmos-diagnostics'
  scope: cosmosAccount
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      { category: 'DataPlaneRequests', enabled: true }
      { category: 'ControlPlaneRequests', enabled: true }
      { category: 'QueryRuntimeStatistics', enabled: true }
    ]
  }
}

output endpoint string = cosmosAccount.properties.documentEndpoint
output accountId string = cosmosAccount.id
