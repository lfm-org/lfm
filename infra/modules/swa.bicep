// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

@description('Azure region')
param location string

@description('Static Web App name')
param swaName string

@description('Log Analytics workspace resource ID for diagnostic settings')
param logAnalyticsWorkspaceId string

@description('Resource tags')
param tags object

resource staticWebApp 'Microsoft.Web/staticSites@2024-04-01' = {
  name: swaName
  location: location
  tags: tags
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

resource swaDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'swa-diagnostics'
  scope: staticWebApp
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      { category: 'StaticSiteDiagnosticLogs', enabled: true }
      { category: 'StaticSiteHttpLogs', enabled: true }
    ]
  }
}

output defaultHostname string = staticWebApp.properties.defaultHostname
output swaId string = staticWebApp.id
