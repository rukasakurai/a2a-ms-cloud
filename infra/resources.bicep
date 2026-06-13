@description('Location for all resources.')
param location string

@description('Tags applied to all resources.')
param tags object = {}

@description('Name of the Foundry (Cognitive Services / AIServices) account.')
param accountName string

@description('Name of the Foundry project.')
param projectName string

@description('Name of the model to deploy.')
param modelName string

@description('Version of the model to deploy.')
param modelVersion string

@description('Name of the model deployment.')
param modelDeploymentName string

@description('Throughput (TPM in thousands) for the model deployment. The default keeps the agent-to-agent demo runnable; capacity 1 causes immediate HTTP 429s under the coordinator\'s nested calls.')
param modelCapacity int = 30

@description('Principal to grant Foundry data-plane access. Empty skips the role assignment.')
param principalId string = ''

@description('Type of the principal referenced by principalId.')
param principalType string = 'User'

// Modern Microsoft Foundry resource: Microsoft.CognitiveServices/accounts (kind AIServices).
resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: accountName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    // Required for Foundry projects (the agent-centric Foundry resource model).
    allowProjectManagement: true
    customSubDomainName: accountName
    publicNetworkAccess: 'Enabled'
    // Use Microsoft Entra ID (DefaultAzureCredential) instead of API keys.
    disableLocalAuth: true
  }
}

resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: account
  name: modelDeploymentName
  sku: {
    name: 'GlobalStandard'
    capacity: modelCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: account
  name: projectName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: projectName
    description: 'Foundry project for the agent-to-agent (A2A) demo.'
  }
  // Ensure the model deployment exists before the project is used by the app.
  dependsOn: [
    modelDeployment
  ]
}

// "Azure AI User" lets the principal build agents and call the responses API.
var azureAiUserRoleDefinitionId = '53ca6127-db72-4b80-b1b0-d745d6d5456d'

resource aiUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  name: guid(account.id, principalId, azureAiUserRoleDefinitionId)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', azureAiUserRoleDefinitionId)
    principalId: principalId
    principalType: principalType
  }
}

output accountName string = account.name
output projectName string = project.name
output projectEndpoint string = 'https://${account.name}.services.ai.azure.com/api/projects/${project.name}'
