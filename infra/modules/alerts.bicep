// =============================================================
// Alerts module (model solution for Independent Exercise 1):
// Action Group (email) + Http5xx metric alert as code.
//
// Note: the availability test is NOT here — the Portal wizard
// creates a webtest resource with a synthetic monitoring config
// and a hidden-link tag coupling it to the App Insights resource,
// which is clumsier to express in Bicep. It can be done
// (Microsoft.Insights/webtests), but it is a fair thing to leave
// as a Portal-managed resource in a course project.
// =============================================================

@description('Email address that receives the alert notifications')
param alertEmail string

@description('Resource id of the web app whose metrics are watched')
param webAppId string

param actionGroupName string
param tags object = {}

// --- Action Group: whom to tell ---

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: actionGroupName
  location: 'Global' // action groups are global resources
  tags: tags
  properties: {
    groupShortName: 'cl26mail' // max 12 chars — shown in SMS/email headers
    enabled: true
    emailReceivers: [
      {
        name: 'primary-email'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

// --- Metric alert: the app is returning server errors ---

resource http5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-cloud26-http5xx'
  location: 'global' // metric alerts are global resources
  tags: tags
  properties: {
    description: 'Campus Events is returning server errors'
    severity: 2
    enabled: true
    scopes: [
      webAppId
    ]
    evaluationFrequency: 'PT1M' // evaluate every minute...
    windowSize: 'PT5M'          // ...over the last 5 minutes
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'Http5xxGreaterThanZero'
          metricName: 'Http5xx'
          operator: 'GreaterThan'
          threshold: 0 // course setting — production thresholds come from knowing normal traffic
          timeAggregation: 'Total'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

output actionGroupId string = actionGroup.id
