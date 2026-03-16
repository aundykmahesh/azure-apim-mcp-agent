targetScope = 'resourceGroup'

@description('Azure region for all resources')
param location string = 'australiaeast'

@description('Environment name used as prefix for resources')
param environmentName string

@description('Container image tag')
param containerImageTag string = 'latest'

@description('Existing APIM service name to create the MCP API in')
param apimServiceName string

@description('Target APIM instances the Functions app will query')
param targetApimInstances array

@description('Azure OpenAI endpoint for the chat agent')
param azureOpenAIEndpoint string = ''

@description('Azure OpenAI deployment name for the chat agent')
param azureOpenAIDeploymentName string = ''

@description('Azure OpenAI Cognitive Services account name (for role assignment)')
param azureOpenAICognitiveAccountName string = ''

@description('Whether to create role assignments (set to false if they already exist)')
param createRoleAssignments bool = false

// ── Managed Identity ──────────────────────────────────────────
module identity 'modules/managed-identity.bicep' = {
  name: 'managed-identity'
  params: {
    name: '${environmentName}-identity'
    location: location
  }
}

// ── Container Registry ────────────────────────────────────────
module acr 'modules/container-registry.bicep' = {
  name: 'container-registry'
  params: {
    name: 'aundyfirstregistry'
    location: location
    identityPrincipalId: identity.outputs.principalId
  }
}

// ── Storage Account (for Azure Functions host) ────────────────
module storage 'modules/storage-account.bicep' = {
  name: 'storage-account'
  params: {
    name: replace('${environmentName}sa', '-', '')
    location: location
    identityPrincipalId: identity.outputs.principalId
  }
}

// ── Container App Environment ─────────────────────────────────
module appEnv 'modules/container-app-environment.bicep' = {
  name: 'container-app-env'
  params: {
    name: '${environmentName}-env'
    location: location
  }
}

// ── Container App (Functions) ─────────────────────────────────
module app 'modules/container-app.bicep' = {
  name: 'container-app'
  params: {
    name: '${environmentName}-app'
    location: location
    environmentId: appEnv.outputs.environmentId
    containerImage: '${acr.outputs.acrLoginServer}/${environmentName}:${containerImageTag}'
    identityId: identity.outputs.identityId
    identityClientId: identity.outputs.clientId
    acrLoginServer: acr.outputs.acrLoginServer
    apimInstances: targetApimInstances
    azureOpenAIEndpoint: azureOpenAIEndpoint
    azureOpenAIDeploymentName: azureOpenAIDeploymentName
    storageAccountName: storage.outputs.storageAccountName
  }
}

// ── RBAC Role Assignments ─────────────────────────────────────
module roles 'modules/role-assignments.bicep' = if (createRoleAssignments) {
  name: 'role-assignments'
  params: {
    principalId: identity.outputs.principalId
    targetApimInstances: targetApimInstances
    azureOpenAICognitiveAccountName: azureOpenAICognitiveAccountName
  }
}

// ── API Management (import API + MCP configuration) ───────────
module apim 'modules/api-management.bicep' = {
  name: 'api-management'
  params: {
    apimServiceName: apimServiceName
    containerAppFqdn: app.outputs.fqdn
  }
}

// ── Outputs ───────────────────────────────────────────────────
output containerAppFqdn string = app.outputs.fqdn
output acrLoginServer string = acr.outputs.acrLoginServer
output identityClientId string = identity.outputs.clientId
