param principalId string
param targetApimInstances array

// API Management Service Reader role
var apimReaderRoleId = '71522526-b88f-4d52-b57f-d31fc3546d0d'

resource roleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for (instance, i) in targetApimInstances: {
    name: guid(subscription().id, principalId, apimReaderRoleId, string(i))
    properties: {
      roleDefinitionId: subscriptionResourceId(
        'Microsoft.Authorization/roleDefinitions',
        apimReaderRoleId
      )
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]
