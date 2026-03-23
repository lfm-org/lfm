@description('Static Web App resource ID')
param swaId string

@description('Function App name')
param functionAppName string

@description('Frontend custom domain')
param customDomainFrontend string

@description('API custom domain')
param customDomainApi string

resource swa 'Microsoft.Web/staticSites@2023-12-01' existing = {
  name: last(split(swaId, '/'))
}

resource swaCustomDomain 'Microsoft.Web/staticSites/customDomains@2023-12-01' = {
  parent: swa
  name: customDomainFrontend
  properties: {}
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' existing = {
  name: functionAppName
}

resource functionsCustomDomain 'Microsoft.Web/sites/hostNameBindings@2023-12-01' = {
  parent: functionApp
  name: customDomainApi
  properties: {
    siteName: functionAppName
  }
}

// Managed certificates:
// - SWA: handled automatically by the staticSites/customDomains resource.
// - Functions: managed cert + SNI binding provisioned by deploy-infra.yml workflow.
