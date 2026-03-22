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

// Note: After this binding is created, enable a managed certificate for the
// Function App custom domain via Azure Portal or CLI:
// az webapp config ssl create --resource-group lfm --name lfm-functions --hostname lfm-api.dinosauruskeksi.com
// Then bind it:
// az webapp config ssl bind --resource-group lfm --name lfm-functions --certificate-thumbprint <thumbprint> --ssl-type SNI
// SWA managed certs are handled automatically by the staticSites/customDomains resource.
