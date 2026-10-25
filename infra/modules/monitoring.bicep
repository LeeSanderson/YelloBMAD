// Logs, the operator's alert channel, the rate-limit refusal signal and the GBP 30 budget
// (PRD section 6.3; spine Operations convention).

param location string
param tags object
param operatorEmail string
param budgetStartDate string
param monthlyBudget int

// Stdout logs from the Container Apps environment land here (AR-34). The daily cap keeps ingestion
// inside the free allowance, so logging cannot breach the cost ceiling; past the cap, ingestion stops
// until the next day rather than billing.
resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-yello'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: json('0.1')
    }
  }
}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-yello-operator'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'yello-op'
    enabled: true
    emailReceivers: [
      {
        name: 'operator'
        emailAddress: operatorEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

// AR-39's second signal: password-hashing rate-limit refusals (open question 14 (a)). The Host writes
// one `RateLimitRefused` line per refused request (Yello.Host/RateLimitLog.cs); this counts them.
// Evaluated every 15 minutes, the cheapest frequency; its monthly cost is in the cost table in
// docs/operations.md.
resource rateLimitAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alert-yello-rate-limit-refusals'
  location: location
  tags: tags
  properties: {
    displayName: 'Yello: sign-in or registration rate-limit refusals'
    severity: 3
    enabled: true
    scopes: [
      workspace.id
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          query: 'ContainerAppConsoleLogs_CL | where ContainerAppName_s == "yello-host" | where Log_s has "RateLimitRefused"'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 20
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
}

// The cost ceiling as a budget on the resource group, alerting at 50, 80 and 100% (open question 16).
resource budget 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: 'budget-yello-monthly'
  properties: {
    category: 'Cost'
    amount: monthlyBudget
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: budgetStartDate
    }
    notifications: {
      fiftyPercent: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 50
        contactEmails: [
          operatorEmail
        ]
      }
      eightyPercent: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 80
        contactEmails: [
          operatorEmail
        ]
      }
      hundredPercent: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        contactEmails: [
          operatorEmail
        ]
      }
    }
  }
}

output workspaceName string = workspace.name
output actionGroupId string = actionGroup.id
