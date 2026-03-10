param principalId string
param targetApimInstances array
param azureOpenAICognitiveAccountName string = ''

// API Management Service Reader role
var apimReaderRoleId = '71522526-b88f-4d52-b57f-d31fc3546d0d'

// Cognitive Services OpenAI User role
var cognitiveOpenAIUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'

// Get unique subscriptions from target APIM instances
var uniqueSubscriptions = union(
  map(targetApimInstances, instance => instance.subscriptionId),
  []
)

resource openAIAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' existing = if (!empty(azureOpenAICognitiveAccountName)) {
  name: azureOpenAICognitiveAccountName
}

// Create unique role assignments at subscription scope (one per unique subscription)
resource roleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for (subId, i) in uniqueSubscriptions: {
    name: guid(subId, principalId, apimReaderRoleId, 'apim-reader')
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

resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(azureOpenAICognitiveAccountName)) {
  scope: openAIAccount
  name: guid(openAIAccount.id, principalId, cognitiveOpenAIUserRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      cognitiveOpenAIUserRoleId
    )
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
