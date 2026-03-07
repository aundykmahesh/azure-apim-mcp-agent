targetScope = 'resourceGroup'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Environment name used as prefix for resources')
param environmentName string

@description('Container image tag')
param containerImageTag string = 'latest'

@description('Existing APIM service name to create the MCP API in')
param apimServiceName string

@description('Target APIM instances the Functions app will query')
param targetApimInstances array

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
    name: replace('${environmentName}acr', '-', '')
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
  }
}

// ── RBAC Role Assignments ─────────────────────────────────────
module roles 'modules/role-assignments.bicep' = {
  name: 'role-assignments'
  params: {
    principalId: identity.outputs.principalId
    targetApimInstances: targetApimInstances
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
