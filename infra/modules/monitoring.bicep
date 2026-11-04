param location string
param logAnalyticsName string
param appInsightsName string
param tags object = {}

// --- Log Analytics Workspace: one store for all telemetry ---

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018' // pay per ingested GB (first 5 GB/month free)
    }
    retentionInDays: 30
  }
  tags: tags
}

// --- Application Insights (workspace-based) ---

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
  tags: tags
}

output connectionString string = appInsights.properties.ConnectionString
output appInsightsId string = appInsights.id
