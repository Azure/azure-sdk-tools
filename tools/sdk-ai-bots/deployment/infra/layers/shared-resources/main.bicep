targetScope = 'resourceGroup'

// ── Resource-name overrides ─────────────────────────────────────────────────
// Every resource this layer creates gets a parameter with a sensible default so
// callers (main.bicep, per-env parameters JSON) can point Bicep at existing
// resources without a rename — e.g. prod's `azure-sdk-qa-bot` RG was manually
// built and has different naming from the generated dev/preview environments.

@description('Name of the shared user-assigned managed identity.')
param managedIdentityNameOverride string = ''

@description('Name of the shared action group.')
param actionGroupNameOverride string = ''

@description('Name of the shared App Configuration store.')
param appConfigNameOverride string = ''

@description('Name of the shared Azure AI Search service.')
param searchServiceNameOverride string = ''

@description('Name of the shared container registry.')
param containerRegistryNameOverride string = ''

@description('Name of the shared storage account.')
param storageAccountNameOverride string = ''

@description('Name of the shared Cosmos DB account.')
param cosmosDbAccountNameOverride string = ''

@description('Azure region for all resources in this layer.')
param location string = 'westus2'

@description('Azure region for the Cosmos DB account. Defaults to `location`; override when the primary region has AZ capacity constraints.')
param cosmosDbLocation string = location

@description('User-assigned identities attached to the Search service, keyed by full resource ID.')
param searchUserAssignedIdentities object = {}

@description('User-assigned identities attached to the container registry, keyed by full resource ID.')
param containerRegistryUserAssignedIdentities object = {}

@description('Treat the container registry as externally managed. Use this when preserving linked identities that the deployer cannot reassign.')
param containerRegistryIsExisting bool = false

@description('Cosmos DB account capabilities to preserve, such as EnableServerless and EnableNoSQLVectorSearch.')
param cosmosCapabilities array = []

@description('AI Search knowledge retrieval tier.')
param searchKnowledgeRetrieval string = 'standard'

@description('Whether the experience-episodes container uses the vector embedding and index contract.')
param enableEpisodeVectorIndex bool = false

var suffix = substring(uniqueString(resourceGroup().id), 0, 6)
var managedIdentityName = !empty(managedIdentityNameOverride) ? managedIdentityNameOverride : 'qabot-identity-${suffix}'
var actionGroupName = !empty(actionGroupNameOverride) ? actionGroupNameOverride : 'qabot-alert-${suffix}'
var appConfigName = !empty(appConfigNameOverride) ? appConfigNameOverride : 'qabot-config-${suffix}'
var searchServiceName = !empty(searchServiceNameOverride) ? searchServiceNameOverride : 'qabot-search-${suffix}'
var containerRegistryName = !empty(containerRegistryNameOverride) ? containerRegistryNameOverride : 'qabotcontainer${suffix}'
var storageAccountName = !empty(storageAccountNameOverride) ? storageAccountNameOverride : 'qabotstorage${suffix}'
var cosmosDbAccountName = !empty(cosmosDbAccountNameOverride) ? cosmosDbAccountNameOverride : 'qabot-db-${suffix}'

// User-assigned managed identity for the QA bot app. Its principalId (Entra object ID)
// is referenced below to grant the app data-plane access to Cosmos DB.
resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2025-05-31-preview' = {
  name: managedIdentityName
  location: location
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

resource configurationStore 'Microsoft.AppConfiguration/configurationStores@2025-08-01-preview' = {
  name: appConfigName
  location: location
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
  location: location
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
    knowledgeRetrieval: searchKnowledgeRetrieval
    disableLocalAuth: false
  }
  sku: {
    name: 'standard'
  }
  identity: {
    type: empty(searchUserAssignedIdentities) ? 'SystemAssigned' : 'SystemAssigned, UserAssigned'
    userAssignedIdentities: searchUserAssignedIdentities
  }
}

resource registry 'Microsoft.ContainerRegistry/registries@2026-01-01-preview' = if (!containerRegistryIsExisting) {
  name: containerRegistryName
  location: location
  identity: {
    type: empty(containerRegistryUserAssignedIdentities) ? 'None' : 'UserAssigned'
    userAssignedIdentities: containerRegistryUserAssignedIdentities
  }
  sku: {
    name: 'Standard'
  }
}

resource existingRegistry 'Microsoft.ContainerRegistry/registries@2026-01-01-preview' existing = if (containerRegistryIsExisting) {
  name: containerRegistryName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2026-04-01' = {
  name: storageAccountName
  location: location
  properties: {
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

resource wikiContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2026-04-01' = {
  name: 'wiki'
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
        locationName: cosmosDbLocation
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    cors: []
    capabilities: cosmosCapabilities
    ipRules: []
    backupPolicy: {
      type: 'Continuous'
      continuousModeProperties: {
        tier: 'Continuous7Days'
      }
    }
    networkAclBypassResourceIds: []
  }
  location: cosmosDbLocation
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
          '/mapping_key'
        ]
        kind: 'Hash'
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
          '/conversation_partition'
        ]
        kind: 'Hash'
      }
    }
  }
}

resource container8 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2026-03-15' = {
  name: 'experience-episodes'
  parent: sqlDatabase
  properties: {
    resource: union({
      id: 'experience-episodes'
      indexingPolicy: enableEpisodeVectorIndex ? {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/embedding/*'
          }
          {
            path: '/"_etag"/?'
          }
        ]
        vectorIndexes: [
          {
            path: '/embedding'
            type: 'quantizedFlat'
          }
        ]
      } : {
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
          '/tenant_id'
        ]
        kind: 'Hash'
        version: 2
      }
    }, enableEpisodeVectorIndex ? {
      vectorEmbeddingPolicy: {
        vectorEmbeddings: [
          {
            path: '/embedding'
            dataType: 'float32'
            distanceFunction: 'cosine'
            dimensions: 1536
          }
        ]
      }
    } : {})
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
          '/sourceChannelId'
        ]
        kind: 'Hash'
        version: 2
      }
    }
  }
}

resource qaRecordsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2026-03-15' = {
  name: 'qa-records'
  parent: sqlDatabase
  properties: {
    resource: {
      id: 'qa-records'
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
          '/tenant_id'
        ]
        kind: 'Hash'
        version: 2
      }
    }
  }
}

resource feedbackRecordsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2026-03-15' = {
  name: 'feedback-records'
  parent: sqlDatabase
  properties: {
    resource: {
      id: 'feedback-records'
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
          '/tenant_id'
        ]
        kind: 'Hash'
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

// Runtime and deployment roles are reconciled by the layer postprovision hook.

// Outputs consumed by downstream azd layers and hooks.
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistryIsExisting ? existingRegistry!.properties.loginServer : registry!.properties.loginServer
output CONTAINER_REGISTRY_NAME string = containerRegistryName
output MANAGED_IDENTITY_NAME string = userAssignedIdentity.name
output STORAGE_ACCOUNT_NAME string = storageAccount.name

@description('Primary blob service endpoint of the shared storage account.')
output STORAGE_BLOB_ENDPOINT string = storageAccount.properties.primaryEndpoints.blob

@description('Client ID of the user-assigned managed identity (qabot-identity).')
output MANAGED_IDENTITY_CLIENT_ID string = userAssignedIdentity.properties.clientId

@description('Resource ID of the user-assigned managed identity (qabot-identity).')
output MANAGED_IDENTITY_RESOURCE_ID string = userAssignedIdentity.id

@description('Principal (object) ID of the user-assigned managed identity (qabot-identity).')
output MANAGED_IDENTITY_PRINCIPAL_ID string = userAssignedIdentity.properties.principalId

@description('App Configuration store name.')
output APP_CONFIG_NAME string = configurationStore.name
output AZURE_APPCONFIG_ENDPOINT string = 'https://${configurationStore.name}.azconfig.io'

@description('Azure AI Search service name.')
output SEARCH_SERVICE_NAME string = searchService.name

@description('Principal (object) ID of the Azure AI Search system-assigned identity.')
output SEARCH_SERVICE_PRINCIPAL_ID string = searchService.identity.principalId

@description('Cosmos DB account name.')
output COSMOS_DB_ACCOUNT_NAME string = databaseAccount.name

@description('Shared action group name.')
output ACTION_GROUP_NAME string = actionGroup.name
