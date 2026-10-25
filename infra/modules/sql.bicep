// The SQL logical server and the one database, on the Azure SQL Database free offer, with the
// free-allowance alert (AC5; spine Operations convention).
//
// Free offer (https://learn.microsoft.com/azure/azure-sql/database/free-offer, read 2026-10-25):
// 100,000 vCore seconds, 32 GB data and 32 GB backup per database per month; "Behavior when free
// limit reached" is AutoPause (paused until next month) or BillOverUsage - and once BillOverUsage is
// chosen it cannot be switched back. Microsoft's own suggestion is a no-cost alert on "Free amount
// remaining" at 10,000 vCore seconds, 10% of the month.
//
// NO DATABASE-LEVEL COLLATION. AD-15 sets collation per column in the migrations, and Azure SQL
// cannot change a database's collation after creation.

param location string
param tags object
param serverName string
param entraAdminObjectId string
param entraAdminLogin string
param administratorLogin string

@secure()
param administratorPassword string

param actionGroupId string

// SQL authentication is on, beside the Entra administrator: the two runtime identities connect as
// contained database users with passwords held only in Key Vault (open question 8 (b)), because
// Microsoft.Data.SqlClient 7 needs a further package for Entra managed-identity authentication.
resource server 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: entraAdminLogin
      sid: entraAdminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: false
    }
  }
}

// Container Apps on the consumption plan has no fixed outbound address, so the server admits Azure
// services. The trade-off is recorded in docs/operations.md.
resource allowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: server
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: server
  name: 'yello'
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 15
    minCapacity: json('0.5')
    requestedBackupStorageRedundancy: 'Local'
  }
}

// AC5: fires when the free amount remaining reaches 10% of the month's 100,000 vCore seconds.
resource freeAllowanceAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-yello-free-vcore-remaining'
  location: 'global'
  tags: tags
  properties: {
    description: 'The Azure SQL free offer has 10% or less of this month\'s vCore seconds left. At zero the database pauses until next month.'
    severity: 2
    enabled: true
    scopes: [
      database.id
    ]
    evaluationFrequency: 'PT1H'
    windowSize: 'PT1H'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'free-amount-remaining'
          criterionType: 'StaticThresholdCriterion'
          metricNamespace: 'Microsoft.Sql/servers/databases'
          metricName: 'free_amount_remaining'
          timeAggregation: 'Minimum'
          operator: 'LessThanOrEqual'
          threshold: 10000
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroupId
      }
    ]
  }
}

output serverFqdn string = server.properties.fullyQualifiedDomainName
