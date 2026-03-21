@description('Azure region')
param location string

@description('Cosmos DB account name')
param accountName string

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
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: 'sisu-raidcal'
  properties: {
    resource: { id: 'sisu-raidcal' }
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

output endpoint string = cosmosAccount.properties.documentEndpoint
output accountId string = cosmosAccount.id
