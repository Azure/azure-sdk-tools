targetScope = 'resourceGroup'

param location string = resourceGroup().location
param siteName string = 'azsdk-custom-code-metrics'
@description('Individual Microsoft/GitHub alias or Microsoft UPN linked to GitHub. Format checks do not establish cleanup eligibility or hosting approval.')
@minLength(1)
param owner string
@description('Resource purpose for owner tracking; this does not exempt resources from cleanup.')
@minLength(1)
param purpose string = 'Azure SDK custom code metrics'
param bootstrapPrincipalId string
@description('Resource-only provisioning does not grant publishing access. An access administrator must deploy again with this enabled.')
param deployRoleAssignments bool = true
@description('Enable only in an environment whose policy permits anonymous approved reports. Private reporting requires a reader API.')
param publicReports bool = false

var tags = {
  Owners: owner
  Purpose: purpose
  Project: 'Azure SDK custom code metrics'
  Environment: 'Playground'
}
var blobContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')

resource site 'Microsoft.Web/staticSites@2023-12-01' = {
  name: siteName
  location: location
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    allowConfigFileUpdates: true
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'azsdkcm${uniqueString(resourceGroup().id)}'
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: publicReports
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    publicNetworkAccess: 'Enabled'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    isVersioningEnabled: true
    deleteRetentionPolicy: {
      enabled: true
      days: 14
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 14
    }
    cors: {
      corsRules: [
        {
          allowedOrigins: [
            'https://${site.properties.defaultHostname}'
          ]
          allowedMethods: [
            'GET'
            'HEAD'
          ]
          allowedHeaders: [
            'Accept'
            'Content-Type'
            'Cache-Control'
            'Pragma'
          ]
          exposedHeaders: [
            'ETag'
            'Content-Encoding'
            'Cache-Control'
          ]
          maxAgeInSeconds: 3600
        }
      ]
    }
  }
}

resource reports 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'reports'
  properties: {
    publicAccess: publicReports ? 'Blob' : 'None'
  }
}

resource archive 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'archive'
  properties: {
    publicAccess: 'None'
  }
}

resource publisher 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-azsdk-custom-code-metrics'
  location: location
  tags: tags
}

resource publisherReportsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (deployRoleAssignments) {
  name: guid(reports.id, publisher.id, blobContributorRole)
  scope: reports
  properties: {
    roleDefinitionId: blobContributorRole
    principalId: publisher.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource publisherArchiveRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (deployRoleAssignments) {
  name: guid(archive.id, publisher.id, blobContributorRole)
  scope: archive
  properties: {
    roleDefinitionId: blobContributorRole
    principalId: publisher.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource bootstrapReportsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (deployRoleAssignments) {
  name: guid(reports.id, bootstrapPrincipalId, blobContributorRole)
  scope: reports
  properties: {
    roleDefinitionId: blobContributorRole
    principalId: bootstrapPrincipalId
    principalType: 'User'
  }
}

resource bootstrapArchiveRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (deployRoleAssignments) {
  name: guid(archive.id, bootstrapPrincipalId, blobContributorRole)
  scope: archive
  properties: {
    roleDefinitionId: blobContributorRole
    principalId: bootstrapPrincipalId
    principalType: 'User'
  }
}

output siteUrl string = 'https://${site.properties.defaultHostname}'
output storageAccount string = storage.name
output indexUrl string = '${storage.properties.primaryEndpoints.blob}reports/dotnet/index.json'
output publisherClientId string = publisher.properties.clientId
output publisherPrincipalId string = publisher.properties.principalId
output publisherResourceId string = publisher.id
output reportsResourceId string = reports.id
output archiveResourceId string = archive.id
output accessConfigured bool = deployRoleAssignments
output reportsArePublic bool = publicReports
