// The Host as a Container App, and the migration bundle as a Container Apps Job (AC1, AC6, AC11,
// AC15; AD-7, AD-14; spine Deployment seed).

param location string
param tags object
param hostImage string
param migrateImage string
param minReplicas int
param registryServer string
param appIdentityId string
param migrateIdentityId string
param keyVaultUri string
param clientOrigin string
param workspaceName string

// Versionless secret URIs: a rotated secret is picked up by the next revision without editing this.
var appSecretUrl = '${keyVaultUri}secrets/yello-app-connection'
var migrateSecretUrl = '${keyVaultUri}secrets/yello-migrate-connection'

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: workspaceName
}

// Consumption only: no dedicated workload profile, nothing billed while scaled to zero.
resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-yello'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: workspace.properties.customerId
        sharedKey: workspace.listKeys().primarySharedKey
      }
    }
  }
}

resource host 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'yello-host'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      // One revision at a time: a new image creates and activates a new revision. The platform
      // briefly overlaps the old and new process during the swap (deferred-work.md, story 7.3).
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        // TLS ends here. The Host learns the original scheme and the client's address from the
        // forwarded headers this ingress appends (ForwardedHeaders__Enabled below).
        allowInsecure: false
      }
      registries: [
        {
          server: registryServer
          identity: appIdentityId
        }
      ]
      secrets: [
        {
          name: 'yello-app-connection'
          keyVaultUrl: appSecretUrl
          identity: appIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'yello-host'
          image: hostImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_HTTP_PORTS'
              value: '8080'
            }
            {
              name: 'ForwardedHeaders__Enabled'
              value: 'true'
            }
            {
              name: 'ClientOrigin'
              value: clientOrigin
            }
            {
              name: 'ConnectionStrings__yello'
              secretRef: 'yello-app-connection'
            }
          ]
          // Both answered from process state, with no database round trip (AC6).
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
              }
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
              }
              periodSeconds: 10
            }
          ]
        }
      ]
      // AD-14: at most one replica.
      scale: {
        minReplicas: minReplicas
        maxReplicas: 1
      }
    }
  }
}

// The migration bundle, run by the deploy workflow before the Host's revision is updated, and only
// then (AR-36, AR-38). Manual trigger: nothing starts it on a schedule.
resource migrate 'Microsoft.App/jobs@2024-03-01' = {
  name: 'yello-migrate'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${migrateIdentityId}': {}
    }
  }
  properties: {
    environmentId: environment.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 0
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: [
        {
          server: registryServer
          identity: migrateIdentityId
        }
      ]
      secrets: [
        {
          name: 'yello-migrate-connection'
          keyVaultUrl: migrateSecretUrl
          identity: migrateIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'yello-migrate'
          image: migrateImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ConnectionStrings__yello'
              secretRef: 'yello-migrate-connection'
            }
          ]
        }
      ]
    }
  }
}

output hostFqdn string = host.properties.configuration.ingress.fqdn
