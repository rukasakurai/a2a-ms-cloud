targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the azd environment, used to derive resource names.')
param environmentName string

@minLength(1)
@description('Primary location for all resources.')
param location string

@description('Id of the user or service principal to assign Foundry data-plane access. Provided automatically by azd.')
param principalId string = ''

@description('Type of the principal referenced by principalId.')
@allowed([
  'User'
  'ServicePrincipal'
])
param principalType string = 'User'

@description('Name of the model to deploy.')
param modelName string = 'gpt-4.1-mini'

@description('Version of the model to deploy.')
param modelVersion string = '2025-04-14'

@description('Name of the model deployment used by the application.')
param modelDeploymentName string = 'gpt-4.1-mini'

@description('Throughput (TPM in thousands) for the model deployment. The default keeps the demo runnable; capacity 1 triggers immediate HTTP 429s.')
param modelCapacity int = 30

var abbrs = {
  account: 'aif'
  project: 'proj'
}
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = {
  'azd-env-name': environmentName
}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  name: 'resources'
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    accountName: '${abbrs.account}-${resourceToken}'
    projectName: '${abbrs.project}-${resourceToken}'
    modelName: modelName
    modelVersion: modelVersion
    modelDeploymentName: modelDeploymentName
    modelCapacity: modelCapacity
    principalId: principalId
    principalType: principalType
  }
}

// Outputs are written to the azd environment (.azure/<env>/.env) and consumed by
// the .NET application via environment variables.
output AZURE_AI_PROJECT_ENDPOINT string = resources.outputs.projectEndpoint
output AZURE_AI_MODEL_DEPLOYMENT_NAME string = modelDeploymentName
output AZURE_AI_FOUNDRY_ACCOUNT_NAME string = resources.outputs.accountName
output AZURE_AI_FOUNDRY_PROJECT_NAME string = resources.outputs.projectName
output AZURE_RESOURCE_GROUP string = resourceGroup.name
