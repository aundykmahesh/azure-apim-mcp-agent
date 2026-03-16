param name string
param location string
param environmentId string
param containerImage string
param identityId string
param identityClientId string
param acrLoginServer string
param apimInstances array
param azureOpenAIEndpoint string = ''
param azureOpenAIDeploymentName string = ''
param storageAccountName string

var baseEnv = [
  {
    name: 'AZURE_CLIENT_ID'
    value: identityClientId
  }
  {
    name: 'AzureWebJobsStorage__accountName'
    value: storageAccountName
  }
  {
    name: 'AzureWebJobsStorage__credential'
    value: 'managedidentity'
  }
  {
    name: 'AzureWebJobsStorage__clientId'
    value: identityClientId
  }
  {
    name: 'FUNCTIONS_WORKER_RUNTIME'
    value: 'dotnet-isolated'
  }
]

var instanceNameEnv = [for (instance, i) in apimInstances: {
  name: 'Apim__Instances__${i}__Name'
  value: instance.name
}]

var instanceSubscriptionEnv = [for (instance, i) in apimInstances: {
  name: 'Apim__Instances__${i}__SubscriptionId'
  value: instance.subscriptionId
}]

var instanceResourceGroupEnv = [for (instance, i) in apimInstances: {
  name: 'Apim__Instances__${i}__ResourceGroup'
  value: instance.resourceGroup
}]

var instanceServiceNameEnv = [for (instance, i) in apimInstances: {
  name: 'Apim__Instances__${i}__ServiceName'
  value: instance.serviceName
}]

var openAIEnv = !empty(azureOpenAIEndpoint) && !empty(azureOpenAIDeploymentName) ? [
  {
    name: 'AzureOpenAI__Endpoint'
    value: azureOpenAIEndpoint
  }
  {
    name: 'AzureOpenAI__DeploymentName'
    value: azureOpenAIDeploymentName
  }
] : []

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
        external: true
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
            baseEnv,
            instanceNameEnv,
            instanceSubscriptionEnv,
            instanceResourceGroupEnv,
            instanceServiceNameEnv,
            openAIEnv
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
