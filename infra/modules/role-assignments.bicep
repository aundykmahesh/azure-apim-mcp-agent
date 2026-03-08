param principalId string
param targetApimInstances array
param azureOpenAICognitiveAccountName string = ''

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

// Cognitive Services OpenAI User role (5e0bd9bd-7b93-4f28-af87-19fc36ad61bd)
var cognitiveOpenAIUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'

resource openAIAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' existing = if (!empty(azureOpenAICognitiveAccountName)) {
  name: azureOpenAICognitiveAccountName
}

resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(azureOpenAICognitiveAccountName)) {
  name: guid(openAIAccount.id, principalId, cognitiveOpenAIUserRoleId)
  scope: openAIAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      cognitiveOpenAIUserRoleId
    )
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
