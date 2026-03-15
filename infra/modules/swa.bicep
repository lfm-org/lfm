@description('Azure region')
param location string

@description('Static Web App name')
param swaName string

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: swaName
  location: location
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

output defaultHostname string = staticWebApp.properties.defaultHostname
output swaId string = staticWebApp.id
