@description('Azure region')
param location string

@description('Cosmos DB account name')
param accountName string

@description('Log Analytics workspace resource ID for diagnostic settings')
param logAnalyticsWorkspaceId string

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: accountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    enableFreeTier: true
    locations: [{ locationName: location, failoverPriority: 0 }]
    consistencyPolicy: { defaultConsistencyLevel: 'Session' }
    capabilities: []
    disableLocalAuth: true
    disableKeyBasedMetadataWriteAccess: true
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: 'lfm'
  properties: {
    resource: { id: 'lfm' }
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
    }
  }
}

resource raidsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'raids'
  properties: {
    resource: {
      id: 'raids'
      partitionKey: { paths: ['/id'], kind: 'Hash' }
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

resource migrationsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'migrations'
  properties: {
    resource: {
      id: 'migrations'
      partitionKey: { paths: ['/id'], kind: 'Hash' }
    }
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
    ]
  }
}

output endpoint string = cosmosAccount.properties.documentEndpoint
output accountId string = cosmosAccount.id
