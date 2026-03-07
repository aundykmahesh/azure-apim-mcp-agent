param name string
param location string
param environmentId string
param containerImage string
param identityId string
param identityClientId string
param acrLoginServer string
param apimInstances array

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 80
        transport: 'http'
      }
      registries: [
        {
          server: acrLoginServer
          identity: identityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'apim-mcp-functions'
          image: containerImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: concat(
            [
              {
                name: 'AZURE_CLIENT_ID'
                value: identityClientId
              }
              {
                name: 'AzureWebJobsStorage'
                value: 'UseDevelopmentStorage=false'
              }
              {
                name: 'FUNCTIONS_WORKER_RUNTIME'
                value: 'dotnet-isolated'
              }
            ],
            [for (instance, i) in apimInstances: {
              name: 'Apim__Instances__${i}__Name'
              value: instance.name
            }],
            [for (instance, i) in apimInstances: {
              name: 'Apim__Instances__${i}__SubscriptionId'
              value: instance.subscriptionId
            }],
            [for (instance, i) in apimInstances: {
              name: 'Apim__Instances__${i}__ResourceGroup'
              value: instance.resourceGroup
            }],
            [for (instance, i) in apimInstances: {
              name: 'Apim__Instances__${i}__ServiceName'
              value: instance.serviceName
            }]
          )
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 3
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

output fqdn string = app.properties.configuration.ingress.fqdn
output appName string = app.name
