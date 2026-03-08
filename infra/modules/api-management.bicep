param apimServiceName string
param containerAppFqdn string

resource apimService 'Microsoft.ApiManagement/service@2022-08-01' existing = {
  name: apimServiceName
}

resource mcpBackend 'Microsoft.ApiManagement/service/backends@2022-08-01' = {
  parent: apimService
  name: 'apim-mcp-backend'
  properties: {
    url: 'https://${containerAppFqdn}'
    protocol: 'http'
  }
}

resource mcpApi 'Microsoft.ApiManagement/service/apis@2022-08-01' = {
  parent: apimService
  name: 'apim-mcp-agent'
  properties: {
    displayName: 'APIM MCP Agent'
    description: 'REST API for querying Azure API Management instances. Exposed as MCP tools for AI agents.'
    path: 'mcp-agent'
    protocols: [
      'https'
    ]
    subscriptionRequired: true
    serviceUrl: 'https://${containerAppFqdn}'
  }
}

// GET /instances
resource listInstancesOp 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  parent: mcpApi
  name: 'listInstances'
  properties: {
    displayName: 'List APIM Instances'
    description: 'Lists all configured Azure API Management instances available for querying.'
    method: 'GET'
    urlTemplate: '/instances'
    responses: [
      {
        statusCode: 200
        description: 'Array of APIM instance names'
      }
    ]
  }
}

// GET /instances/{instanceName}/apis
resource listApisOp 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  parent: mcpApi
  name: 'listApis'
  properties: {
    displayName: 'List APIs'
    description: 'Lists all APIs registered in a specific APIM instance.'
    method: 'GET'
    urlTemplate: '/instances/{instanceName}/apis'
    templateParameters: [
      {
        name: 'instanceName'
        description: 'Name of the APIM instance to query'
        type: 'string'
        required: true
      }
    ]
  }
}

// GET /instances/{instanceName}/apis/search?keyword={keyword}
resource searchApisOp 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  parent: mcpApi
  name: 'searchApis'
  properties: {
    displayName: 'Search APIs'
    description: 'Searches APIs by keyword across name, description, and path in the specified APIM instance.'
    method: 'GET'
    urlTemplate: '/instances/{instanceName}/apis/search?keyword={keyword}'
    templateParameters: [
      {
        name: 'instanceName'
        description: 'Name of the APIM instance to search'
        type: 'string'
        required: true
      }
      {
        name: 'keyword'
        description: 'Search keyword to match against API name, description, or path'
        type: 'string'
        required: true
      }
    ]
  }
}

// GET /instances/{instanceName}/apis/{apiId}
resource getApiDetailsOp 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  parent: mcpApi
  name: 'getApiDetails'
  properties: {
    displayName: 'Get API Details'
    description: 'Gets detailed metadata for a specific API including description, service URL, and supported protocols.'
    method: 'GET'
    urlTemplate: '/instances/{instanceName}/apis/{apiId}'
    templateParameters: [
      {
        name: 'instanceName'
        description: 'Name of the APIM instance'
        type: 'string'
        required: true
      }
      {
        name: 'apiId'
        description: 'The API display name or internal name'
        type: 'string'
        required: true
      }
    ]
  }
}

// GET /instances/{instanceName}/apis/{apiId}/spec
resource downloadSpecOp 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  parent: mcpApi
  name: 'downloadApiSpec'
  properties: {
    displayName: 'Download API Spec'
    description: 'Downloads and returns the full OpenAPI specification content for a specific API.'
    method: 'GET'
    urlTemplate: '/instances/{instanceName}/apis/{apiId}/spec'
    templateParameters: [
      {
        name: 'instanceName'
        description: 'Name of the APIM instance'
        type: 'string'
        required: true
      }
      {
        name: 'apiId'
        description: 'The API display name or internal name'
        type: 'string'
        required: true
      }
    ]
  }
}

// GET /instances/{instanceName}/apis/{apiId}/operations
resource listApiOperationsOp 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  parent: mcpApi
  name: 'listApiOperations'
  properties: {
    displayName: 'List API Operations'
    description: 'Lists all HTTP operations (endpoints) for a specific API.'
    method: 'GET'
    urlTemplate: '/instances/{instanceName}/apis/{apiId}/operations'
    templateParameters: [
      {
        name: 'instanceName'
        description: 'Name of the APIM instance'
        type: 'string'
        required: true
      }
      {
        name: 'apiId'
        description: 'The API display name or internal name'
        type: 'string'
        required: true
      }
    ]
  }
}

// GET /instances/{instanceName}/catalog
resource getApiCatalogOp 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  parent: mcpApi
  name: 'getApiCatalog'
  properties: {
    displayName: 'Get API Catalog'
    description: 'Returns all APIs in an APIM instance with their operations in a single call.'
    method: 'GET'
    urlTemplate: '/instances/{instanceName}/catalog'
    templateParameters: [
      {
        name: 'instanceName'
        description: 'Name of the APIM instance'
        type: 'string'
        required: true
      }
    ]
  }
}

// GET /health
resource healthOp 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  parent: mcpApi
  name: 'healthCheck'
  properties: {
    displayName: 'Health Check'
    description: 'Returns the health status of the service.'
    method: 'GET'
    urlTemplate: '/health'
    responses: [
      {
        statusCode: 200
        description: 'Service is healthy'
      }
    ]
  }
}

// POST /chat
resource chatOp 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  parent: mcpApi
  name: 'chat'
  properties: {
    displayName: 'Chat with AI Agent'
    description: 'Send a free-text message to the AI agent.'
    method: 'POST'
    urlTemplate: '/chat'
    responses: [
      {
        statusCode: 200
        description: 'AI agent response'
      }
    ]
  }
}

// API policy with backend routing
resource apiPolicy 'Microsoft.ApiManagement/service/apis/policies@2022-08-01' = {
  parent: mcpApi
  name: 'policy'
  properties: {
    format: 'xml'
    value: '''
<policies>
    <inbound>
        <base />
        <set-backend-service backend-id="apim-mcp-backend" />
    </inbound>
    <backend>
        <base />
    </backend>
    <outbound>
        <base />
    </outbound>
    <on-error>
        <base />
    </on-error>
</policies>
'''
  }
}
