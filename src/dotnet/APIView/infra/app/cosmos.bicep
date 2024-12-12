param accountName string
param location string = resourceGroup().location
param tags object = {}
param principalId string
param containers array = [
  {
    name: 'Reviews'
    id: 'Reviews'
    partitionKey: '/id'
  }
  {
    name: 'APIRevisions'
    id: 'APIRevisions'
    partitionKey: '/ReviewId'
  }
  {
    name: 'Comments'
    id: 'Comments'
    partitionKey: '/ReviewId'
  }
  {
    name: 'PullRequests'
    id: 'PullRequests'
    partitionKey: '/ReviewId'
  }
  {
    name: 'SamplesRevisions'
    id: 'SamplesRevisions'
    partitionKey: '/ReviewId'
  }
  {
    name: 'Profiles'
    id: 'Profiles'
    partitionKey: '/id'
  }
]

param databaseName string = ''

// Because databaseName is optional in main.bicep, we make sure the database name is set here.
var defaultDatabaseName = 'APIViewV2'
var actualDatabaseName = !empty(databaseName) ? databaseName : defaultDatabaseName

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: accountName
  kind: 'GlobalDocumentDB'
  location: location
  tags: tags
  properties: {
    consistencyPolicy: { defaultConsistencyLevel: 'Session' }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    databaseAccountOfferType: 'Standard'
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    apiProperties: {}
    capabilities: [{ name: 'EnableServerless' }]
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: account
  name: databaseName
  properties: {
    resource: { id: databaseName }
  }

  resource list 'containers' = [
    for container in containers: {
      name: container.name
      properties: {
        resource: {
          id: container.id
          partitionKey: { paths: [container.partitionKey] }
        }
        options: {}
      }
    }
  ]
}

resource database_legacy 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: account
  name: 'APIView'
  properties: {
    resource: { id: 'APIView' }
  }

  resource list 'containers' = [
    for container in containers: {
      name: container.name
      properties: {
        resource: {
          id: container.id
          partitionKey: { paths: [container.partitionKey] }
        }
        options: {}
      }
    }
  ]
}

var roleDefinitionId = guid('contributor-role-definition-', principalId, account.id)
var roleAssignmentId = guid(roleDefinitionId, principalId, account.id)

resource contributorRoleDefinition 'Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions@2021-04-15' = {
  parent: account
  name: guid('contributor-role-definition-', principalId, account.id)
  properties: {
    roleName: 'Contributor'
    type: 'CustomRole'
    assignableScopes: [account.id]
    permissions: [
      {
        dataActions: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/*'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/items/*'
        ]
        notDataActions: []
      }
    ]
  }
}

resource sqlRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2021-04-15' = {
  parent: account
  name: roleAssignmentId
  properties: {
    roleDefinitionId: contributorRoleDefinition.id
    principalId: principalId
    scope: account.id
  }
}

output databaseName string = actualDatabaseName
output endpoint string = account.properties.documentEndpoint
