targetScope = 'resourceGroup'

// ── Resource-name overrides ─────────────────────────────────────────────────
// Every resource this module creates gets a param with a sensible default so
// callers (main.bicep, per-env parameters JSON) can point Bicep at existing
// resources without a rename — e.g. prod's `azure-sdk-qa-bot` RG was manually
// built and has different naming from the generated dev/preview environments.

@description('Name of the shared user-assigned managed identity.')
param managedIdentityName string = 'qabot-identity-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the shared action group.')
param actionGroupName string = 'qabot-alert-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the shared Key Vault.')
param keyVaultName string = 'qabot-keyvault-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the shared App Configuration store.')
param appConfigName string = 'qabot-config-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the shared Azure AI Search service.')
param searchServiceName string = 'qabot-search-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the shared container registry.')
param containerRegistryName string = 'qabotcontainer${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the shared storage account.')
param storageAccountName string = 'qabotstorage${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the shared Cosmos DB account.')
param cosmosDbAccountName string = 'qabot-db-${substring(uniqueString(resourceGroup().id), 0, 6)}'

// User-assigned managed identity for the QA bot app. Its principalId (Entra object ID)
// is referenced below to grant the app data-plane access to Cosmos DB.
resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2025-05-31-preview' = {
  name: managedIdentityName
  location: 'westus2'
}

resource actionGroup 'Microsoft.Insights/actionGroups@2024-10-01-preview' = {
  name: actionGroupName
  location: 'Global'
  properties: {
    groupShortName: 'Alert'
    enabled: true
    emailReceivers: [
      {
        name: 'Email0_-EmailAction-'
        emailAddress: 'azuresdkqabotproject@service.microsoft.com'
        useCommonAlertSchema: true
      }
    ]
  }
}

resource vault 'Microsoft.KeyVault/vaults@2026-03-01-preview' = {
  name: keyVaultName
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: '72f988bf-86f1-41af-91ab-2d7cd011db47'
    networkAcls: {
      bypass: 'None'
      defaultAction: 'Allow'
    }
    accessPolicies: []
  }
  location: 'westus2'
}

resource configurationStore 'Microsoft.AppConfiguration/configurationStores@2025-08-01-preview' = {
  name: appConfigName
  location: 'westus2'
  properties: {
    encryption: {}
    disableLocalAuth: true
    defaultKeyValueRevisionRetentionPeriodInSeconds: 2592000
    dataPlaneProxy: {
      authenticationMode: 'Local'
      privateLinkDelegation: 'Disabled'
    }
    telemetry: {}
    azureFrontDoor: {}
  }
  sku: {
    name: 'standard'
  }
}

resource searchService 'Microsoft.Search/searchServices@2026-03-01-preview' = {
  name: searchServiceName
  location: 'West US 2'
  properties: {
    computeType: 'Default'
    networkRuleSet: {
      ipRules: []
      bypass: 'None'
    }
    encryptionWithCmk: {
      enforcement: 'Unspecified'
    }
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http401WithBearerChallenge'
      }
    }
    dataExfiltrationProtections: []
    semanticSearch: 'standard'
    knowledgeRetrieval: 'free'
  }
  sku: {
    name: 'standard'
  }
  identity: {
    type: 'SystemAssigned'
  }
}

resource registry 'Microsoft.ContainerRegistry/registries@2026-01-01-preview' = {
  name: containerRegistryName
  location: 'westus2'
  sku: {
    name: 'Standard'
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2026-04-01' = {
  name: storageAccountName
  location: 'westus2'
  properties: {
    dualStackEndpointPreference: {
      publishIpv6Endpoint: false
    }
    dnsEndpointType: 'Standard'
    defaultToOAuthAuthentication: false
    publicNetworkAccess: 'Enabled'
    allowCrossTenantReplication: false
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    largeFileSharesState: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      virtualNetworkRules: []
      ipRules: []
    }
    supportsHttpsTrafficOnly: true
    encryption: {
      requireInfrastructureEncryption: false
      services: {
        file: {
          keyType: 'Account'
          enabled: true
        }
        blob: {
          keyType: 'Account'
          enabled: true
        }
      }
    }
    accessTier: 'Hot'
  }
  sku: {
    name: 'Standard_RAGRS'
  }
  kind: 'StorageV2'
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2026-04-01' = {
  name: 'default'
  parent: storageAccount
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
    cors: {
      corsRules: []
    }
    isVersioningEnabled: false
  }
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2026-04-01' = {
  name: 'bot-configs'
  parent: blobService
  properties: {
    immutableStorageWithVersioning: {
      enabled: false
    }
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'None'
  }
}

resource container2 'Microsoft.Storage/storageAccounts/blobServices/containers@2026-04-01' = {
  name: 'evaluation-dataset'
  parent: blobService
  properties: {
    immutableStorageWithVersioning: {
      enabled: false
    }
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'None'
  }
}

resource container3 'Microsoft.Storage/storageAccounts/blobServices/containers@2026-04-01' = {
  name: 'feedback'
  parent: blobService
  properties: {
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'None'
  }
}

resource container4 'Microsoft.Storage/storageAccounts/blobServices/containers@2026-04-01' = {
  name: 'knowledge'
  parent: blobService
  properties: {
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'None'
  }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2026-04-01' = {
  name: 'default'
  parent: storageAccount
}

resource table 'Microsoft.Storage/storageAccounts/tableServices/tables@2026-04-01' = {
  name: 'TeamsChannelConversationsDev'
  parent: tableService
}

resource table2 'Microsoft.Storage/storageAccounts/tableServices/tables@2026-04-01' = {
  name: 'TeamsChannelConversationsProd'
  parent: tableService
}

resource databaseAccount 'Microsoft.DocumentDB/databaseAccounts@2026-03-15' = {
  name: cosmosDbAccountName
  properties: {
    publicNetworkAccess: 'Enabled'
    enableAutomaticFailover: true
    enableMultipleWriteLocations: false
    isVirtualNetworkFilterEnabled: false
    virtualNetworkRules: []
    disableKeyBasedMetadataWriteAccess: false
    enableFreeTier: false
    enableAnalyticalStorage: false
    analyticalStorageConfiguration: {
      schemaType: 'WellDefined'
    }
    databaseAccountOfferType: 'Standard'
    defaultIdentity: 'FirstPartyIdentity'
    networkAclBypass: 'None'
    disableLocalAuth: true
    enablePartitionMerge: false
    enableBurstCapacity: false
    minimalTlsVersion: 'Tls12'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
      maxIntervalInSeconds: 5
      maxStalenessPrefix: 100
    }
    locations: [
      {
        locationName: 'West US 2'
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    cors: []
    capabilities: []
    ipRules: []
    backupPolicy: {
      type: 'Continuous'
      continuousModeProperties: {
        tier: 'Continuous7Days'
      }
    }
    networkAclBypassResourceIds: []
  }
  location: 'West US 2'
  tags: {
    defaultExperience: 'Core (SQL)'
    'hidden-workload-type': 'Development/Testing'
    'hidden-cosmos-mmspecial': ''
  }
  identity: {
    type: 'None'
  }
  kind: 'GlobalDocumentDB'
}

resource sqlDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2026-03-15' = {
  name: 'azure-sdk-qa-bot'
  parent: databaseAccount
  properties: {
    resource: {
      id: 'azure-sdk-qa-bot'
    }
  }
}

resource container6 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2026-03-15' = {
  name: 'conversation-mappings'
  parent: sqlDatabase
  properties: {
    resource: {
      id: 'conversation-mappings'
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      partitionKey: {
        paths: [
          '/conversationId'
        ]
        kind: 'Hash'
        version: 2
      }
    }
  }
}

resource container7 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2026-03-15' = {
  name: 'conversation-messages'
  parent: sqlDatabase
  properties: {
    resource: {
      id: 'conversation-messages'
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      partitionKey: {
        paths: [
          '/conversationId'
        ]
        kind: 'Hash'
        version: 2
      }
    }
  }
}

resource container8 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2026-03-15' = {
  name: 'experience-episodes'
  parent: sqlDatabase
  properties: {
    resource: {
      id: 'experience-episodes'
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      partitionKey: {
        paths: [
          '/episodeId'
        ]
        kind: 'Hash'
        version: 2
      }
    }
  }
}

resource container9 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2026-03-15' = {
  name: 'thread-mappings'
  parent: sqlDatabase
  properties: {
    resource: {
      id: 'thread-mappings'
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      partitionKey: {
        paths: [
          '/threadId'
        ]
        kind: 'Hash'
        version: 2
      }
    }
  }
}

// ============================================================================
// Azure RBAC role assignments
// ----------------------------------------------------------------------------
// Principals:
//   userAssignedIdentity.properties.principalId -> qabot-identity (app runtime)
//   developerGroupObjectId                       -> AzureSDKChatBot_Developer group
//
// ============================================================================

// AzureSDKChatBot_Developer Entra group object ID.
var developerGroupObjectId = '2efb50ed-0ca9-4cf1-b43b-9b31a87e08f5'

// Built-in role definition IDs.
// Azure RBAC roles are referenced via subscriptionResourceId(...).
// cosmosDbDataContributor is a Cosmos DB data-plane role (built-in, auto-created
// by Azure) referenced via the account's sqlRoleDefinitions, not Azure RBAC.
var roleIds = {
  storageBlobDataOwner: 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
  storageBlobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  storageQueueDataContributor: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
  storageTableDataContributor: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  keyVaultSecretsOfficer: 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
  appConfigurationDataReader: '516239f1-63e1-4d78-a4de-a74fb236a071'
  searchIndexDataContributor: '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
  contributor: 'b24988ac-6180-42a0-ab88-20f7382dd24c'
  cosmosDbDataContributor: '00000000-0000-0000-0000-000000000002'
}

// --- Managed identity (qabot-identity) ------------------------------------

// Blob read/write/delete for app runtime data.
resource identityStorageBlobOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, userAssignedIdentity.id, roleIds.storageBlobDataOwner)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageBlobDataOwner)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Queue access for app runtime.
resource identityStorageQueueContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, userAssignedIdentity.id, roleIds.storageQueueDataContributor)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageQueueDataContributor)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Table access for Teams channel conversation tables.
resource identityStorageTableContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, userAssignedIdentity.id, roleIds.storageTableDataContributor)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageTableDataContributor)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Read secrets from Key Vault at runtime.
resource identityKeyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, userAssignedIdentity.id, roleIds.keyVaultSecretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.keyVaultSecretsUser)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Read configuration values from App Configuration.
resource identityAppConfigDataReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(configurationStore.id, userAssignedIdentity.id, roleIds.appConfigurationDataReader)
  scope: configurationStore
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.appConfigurationDataReader)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Read/write the search index.
resource identitySearchIndexContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchService.id, userAssignedIdentity.id, roleIds.searchIndexDataContributor)
  scope: searchService
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.searchIndexDataContributor)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Manage the container registry (push/pull/admin).
resource identityAcrContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, userAssignedIdentity.id, roleIds.contributor)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.contributor)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Read/write Cosmos DB data at runtime (Cosmos data-plane role).
resource sqlRoleAssignment2 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2026-03-15' = {
  name: guid(databaseAccount.id, userAssignedIdentity.id, roleIds.cosmosDbDataContributor)
  parent: databaseAccount
  properties: {
    roleDefinitionId: '${databaseAccount.id}/sqlRoleDefinitions/${roleIds.cosmosDbDataContributor}'
    principalId: userAssignedIdentity.properties.principalId
    scope: databaseAccount.id
  }
}

// --- AzureSDKChatBot_Developer group ----------------------------------------

// Blob read/write for developers.
resource developerStorageBlobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, developerGroupObjectId, roleIds.storageBlobDataContributor)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageBlobDataContributor)
    principalId: developerGroupObjectId
    principalType: 'Group'
  }
}

// Read configuration values from App Configuration.
resource developerAppConfigDataReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(configurationStore.id, developerGroupObjectId, roleIds.appConfigurationDataReader)
  scope: configurationStore
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.appConfigurationDataReader)
    principalId: developerGroupObjectId
    principalType: 'Group'
  }
}

// Manage Key Vault secrets for developers.
resource developerKeyVaultSecretsOfficer 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, developerGroupObjectId, roleIds.keyVaultSecretsOfficer)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.keyVaultSecretsOfficer)
    principalId: developerGroupObjectId
    principalType: 'Group'
  }
}

// Manage the container registry for developers.
resource developerAcrContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, developerGroupObjectId, roleIds.contributor)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.contributor)
    principalId: developerGroupObjectId
    principalType: 'Group'
  }
}

// Read/write Cosmos DB data for developers (Cosmos data-plane role).
resource sqlRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2026-03-15' = {
  name: guid(databaseAccount.id, developerGroupObjectId, roleIds.cosmosDbDataContributor)
  parent: databaseAccount
  properties: {
    roleDefinitionId: '${databaseAccount.id}/sqlRoleDefinitions/${roleIds.cosmosDbDataContributor}'
    principalId: developerGroupObjectId
    scope: databaseAccount.id
  }
}

// Output
output managedIdentityName string = userAssignedIdentity.name
output storageAccountName string = storageAccount.name

@description('Primary blob service endpoint of the shared storage account.')
output storageBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob
@description('Container registry resource name ')
output containerRegistryName string = registry.name

@description('Client ID of the user-assigned managed identity (qabot-identity).')
output managedIdentityClientId string = userAssignedIdentity.properties.clientId

@description('Resource ID of the user-assigned managed identity (qabot-identity).')
output managedIdentityResourceId string = userAssignedIdentity.id

@description('Principal (object) ID of the user-assigned managed identity (qabot-identity).')
output managedIdentityPrincipalId string = userAssignedIdentity.properties.principalId

@description('Container registry login server')
output containerRegistryLoginServer string = registry.properties.loginServer

@description('Key Vault name.')
output keyVaultName string = vault.name

@description('App Configuration store name.')
output appConfigName string = configurationStore.name

@description('Azure AI Search service name.')
output searchServiceName string = searchService.name

@description('Cosmos DB account name.')
output cosmosDbAccountName string = databaseAccount.name

@description('Shared action group name.')
output actionGroupName string = actionGroup.name
