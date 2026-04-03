@description('Azure region')
param location string

@description('Static Web App name')
param swaName string

@description('Resource tags')
param tags object

resource staticWebApp 'Microsoft.Web/staticSites@2024-04-01' = {
  name: swaName
  location: location
  tags: tags
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

output defaultHostname string = staticWebApp.properties.defaultHostname
output swaId string = staticWebApp.id
