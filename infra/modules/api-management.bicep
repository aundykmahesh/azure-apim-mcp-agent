param apimServiceName string
param containerAppFqdn string
param tenantId string = tenant().tenantId
param audienceClientId string = ''

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
  name: 'apim-mcp-api'
  properties: {
    displayName: 'APIM MCP Agent'
    description: 'REST API for querying Azure API Management instances. Exposed as MCP tools for AI agents.'
    path: 'apim-mcp'
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
  name: 'list-instances'
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
  name: 'list-apis'
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
  name: 'search-apis'
  properties: {
    displayName: 'Search APIs'
    description: 'Searches APIs by keyword across name, description, and path in the specified APIM instance.'
    method: 'GET'
    urlTemplate: '/instances/{instanceName}/apis/search'
    templateParameters: [
      {
        name: 'instanceName'
        description: 'Name of the APIM instance to search'
        type: 'string'
        required: true
      }
    ]
    request: {
      queryParameters: [
        {
          name: 'keyword'
          description: 'Search keyword to match against API name, description, or path'
          type: 'string'
          required: true
        }
      ]
    }
  }
}

// GET /instances/{instanceName}/apis/{apiId}
resource getApiDetailsOp 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  parent: mcpApi
  name: 'get-api-details'
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
  name: 'download-api-spec'
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

// API policy with JWT validation and backend routing
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
