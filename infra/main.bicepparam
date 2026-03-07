using 'main.bicep'

param environmentName = 'apim-mcp'
param containerImageTag = 'latest'

// The existing APIM instance that will front the Functions app as an MCP server
param apimServiceName = 'aundy-apim'

// Target APIM instances the Functions app will query for API metadata
param targetApimInstances = [
  {
    name: 'dev'
    subscriptionId: 'a181ee41-b325-49cc-9866-78cedd19e733'
    resourceGroup: 'aundydev'
    serviceName: 'aundy-apim'
  }
]
