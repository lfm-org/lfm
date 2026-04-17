// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

@description('Function App name (must exist in the same resource group)')
param functionAppName string

@description('Cosmos DB account name (must exist in the same resource group)')
param cosmosAccountName string

@description('Email address for alert notifications')
param alertEmail string

@description('Resource tags')
param tags object

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' existing = {
  name: cosmosAccountName
}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-${functionAppName}'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'lfm-alerts'
    enabled: true
    emailReceivers: [
      { name: 'admin', emailAddress: alertEmail, useCommonAlertSchema: true }
    ]
  }
}

// Note: Http5xx and HttpResponseTime platform metrics are not available on
// Linux Consumption plan. HTTP error and latency monitoring is handled by
// Application Insights (auto-configured on the function app).

// Alert: Cosmos DB request throttling (HTTP 429)
resource cosmosThrottleAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-${cosmosAccountName}-throttle'
  location: 'global'
  tags: tags
  properties: {
    severity: 3
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    autoMitigate: true
    scopes: [cosmosAccount.id]
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'ThrottledRequests'
          metricName: 'TotalRequests'
          metricNamespace: 'Microsoft.DocumentDB/databaseAccounts'
          operator: 'GreaterThan'
          threshold: 10
          timeAggregation: 'Count'
          criterionType: 'StaticThresholdCriterion'
          dimensions: [
            { name: 'StatusCode', operator: 'Include', values: ['429'] }
          ]
        }
      ]
    }
    actions: [{ actionGroupId: actionGroup.id }]
  }
}
