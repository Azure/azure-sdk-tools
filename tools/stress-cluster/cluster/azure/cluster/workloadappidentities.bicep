param groupSuffix string
param location string

param infraNamespace string
param infraWorkloadServiceAccountName string
param workloadAppIssuer string
param workloadAppPoolCount int

resource infraWorkloadApp 'Microsoft.ManagedIdentity/userAssignedIdentities@2022-01-31-preview' = {
  name: 'stress-infra-workload-${groupSuffix}'
  location: location

  resource creds 'federatedIdentityCredentials' = {
    name: 'stress-infra-federated-${groupSuffix}'
    properties: {
      issuer: workloadAppIssuer
      audiences: ['api://AzureADTokenExchange']
      subject: 'system:serviceaccount:${infraNamespace}:${infraWorkloadServiceAccountName}'
    }
  }
}

resource workloadApps 'Microsoft.ManagedIdentity/userAssignedIdentities@2022-01-31-preview' = [for i in range(0, workloadAppPoolCount): {
  name: 'stress-app-workload-${groupSuffix}-${i}'
  location: location
}]

output infraWorkloadAppClientId string = infraWorkloadApp.properties.clientId
output infraWorkloadAppObjectId string = infraWorkloadApp.properties.principalId

output workloadAppInfo array = [for i in range(0, workloadAppPoolCount): {
  name: 'stress-app-workload-${groupSuffix}-${i}'
  clientId: workloadApps[i].properties.clientId
  objectId: workloadApps[i].properties.principalId
}]
