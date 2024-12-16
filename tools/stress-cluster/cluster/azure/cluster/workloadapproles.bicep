targetScope = 'subscription'

param infraWorkloadAppObjectId string
param workloadApps array

var serviceBusDataOwnerRoleId = '090c5cfd-751d-490a-894a-3ce6f1109419'
var eventHubsDataOwnerRoleId = 'f526a384-b230-433a-b45c-95f59c4a2dec'
var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageFileDataContributorRoleId = '69566ab7-960f-475b-8e7c-b3118f30c6bd'
var contributorRoleId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
var userAccessAdministratorRoleId = '18d7d88d-d35e-4fb5-a5c3-7773c20a72d9'

resource infraWorkloadAppContrib 'Microsoft.Authorization/roleAssignments@2021-04-01-preview' = {
  name: guid('infraWorkloadAppContrib', subscription().id, infraWorkloadAppObjectId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributorRoleId)
    principalId: infraWorkloadAppObjectId
    principalType: 'ServicePrincipal'
  }
}

resource infraWorkloadAppUA 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid('infraWorkloadAppUA', subscription().id, infraWorkloadAppObjectId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', userAccessAdministratorRoleId)
    principalId: infraWorkloadAppObjectId
    principalType: 'ServicePrincipal'
  }
}

resource workloadAppContrib 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = [for i in range(0, length(workloadApps)): {
  name: guid('workloadAppContrib', subscription().id, workloadApps[i].objectId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributorRoleId)
    principalId: workloadApps[i].objectId
    principalType: 'ServicePrincipal'
  }
}]
 
resource workloadAppBlobDataOwner 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = [for i in range(0, length(workloadApps)): {
  name: guid('workloadAppBlobDataOwner', subscription().id, workloadApps[i].objectId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
    principalId: workloadApps[i].objectId
    principalType: 'ServicePrincipal'
  }
}]

resource workloadAppFileDataContributor 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = [for i in range(0, length(workloadApps)): {
  name: guid('workloadAppFileDataContributor', subscription().id, workloadApps[i].objectId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageFileDataContributorRoleId)
    principalId: workloadApps[i].objectId
    principalType: 'ServicePrincipal'
  }
}]

resource workloadAppEHDataOwner 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = [for i in range(0, length(workloadApps)): {
  name: guid('workloadAppEHDataOwner', subscription().id, workloadApps[i].objectId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', eventHubsDataOwnerRoleId)
    principalId: workloadApps[i].objectId
    principalType: 'ServicePrincipal'
  }
}]

resource workloadAppSBDataOwner 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = [for i in range(0, length(workloadApps)): {
  name: guid('workloadAppSBDataOwner', subscription().id, workloadApps[i].objectId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataOwnerRoleId)
    principalId: workloadApps[i].objectId
    principalType: 'ServicePrincipal'
  }
}]
