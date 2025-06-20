param storageAccounts_apiviewuitest_name string = 'apiviewuitest'
param databaseAccounts_apiviewuitest_name string = 'apiviewuitest'
param configurationStores_apiviewuikvconfig_name string = 'apiviewuikvconfig'
param virtualnetworks_apiviewuivnet_externalid string = '/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/APIView-UI/providers/Microsoft.Network/virtualnetworks/apiviewuivnet'

resource configurationStores_apiviewuikvconfig_name_resource 'Microsoft.AppConfiguration/configurationStores@2024-05-01' = {
  name: configurationStores_apiviewuikvconfig_name
  location: 'eastus'
  sku: {
    name: 'standard'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    encryption: {}
    disableLocalAuth: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: false
    dataPlaneProxy: {
      authenticationMode: 'Pass-through'
      privateLinkDelegation: 'Disabled'
    }
  }
}

resource databaseAccounts_apiviewuitest_name_resource 'Microsoft.DocumentDB/databaseAccounts@2024-12-01-preview' = {
  name: databaseAccounts_apiviewuitest_name
  location: 'West US 2'
  tags: {
    defaultExperience: 'Core (SQL)'
    'hidden-cosmos-mmspecial': ''
    owners: 'prmarott, chononiw'
    'network-isolation': 'vnet-app'
  }
  kind: 'GlobalDocumentDB'
  identity: {
    type: 'None'
  }
  properties: {
    publicNetworkAccess: 'Enabled'
    enableAutomaticFailover: false
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
    enableMaterializedViews: false
    capacityMode: 'Provisioned'
    defaultIdentity: 'FirstPartyIdentity'
    networkAclBypass: 'None'
    disableLocalAuth: true
    enablePartitionMerge: false
    enablePerRegionPerPartitionAutoscale: true
    enableBurstCapacity: false
    enablePriorityBasedExecution: false
    defaultPriorityLevel: 'High'
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
      type: 'Periodic'
      periodicModeProperties: {
        backupIntervalInMinutes: 1200
        backupRetentionIntervalInHours: 600
        backupStorageRedundancy: 'Geo'
      }
    }
    networkAclBypassResourceIds: []
    diagnosticLogSettings: {
      enableFullTextQuery: 'None'
    }
  }
}

resource storageAccounts_apiviewuitest_name_resource 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: storageAccounts_apiviewuitest_name
  location: 'westus2'
  tags: {
    owners: 'prmarott, chononiw'
    'network-isolation': 'vnet-app'
  }
  sku: {
    name: 'Standard_LRS'
    tier: 'Standard'
  }
  kind: 'StorageV2'
  properties: {
    defaultToOAuthAuthentication: false
    publicNetworkAccess: 'Enabled'
    allowCrossTenantReplication: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    networkAcls: {
      resourceAccessRules: [
        {
          tenantId: '72f988bf-86f1-41af-91ab-2d7cd011db47'
          resourceId: '/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/providers/Microsoft.Security/datascanners/storageDataScanner'
        }
      ]
      bypass: 'AzureServices'
      virtualNetworkRules: [
        {
          id: '${virtualnetworks_apiviewuivnet_externalid}/subnets/default'
          action: 'Allow'
          state: 'Succeeded'
        }
      ]
      ipRules: [
        {
          value: '4.155.25.76'
          action: 'Allow'
        }
        {
          value: '73.53.0.0/16'
          action: 'Allow'
        }
        {
          value: '73.53.83.4'
          action: 'Allow'
        }
      ]
      defaultAction: 'Allow'
    }
    supportsHttpsTrafficOnly: true
    encryption: {
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
      keySource: 'Microsoft.Storage'
    }
    accessTier: 'Hot'
  }
}

resource databaseAccounts_apiviewuitest_name_APIView 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_resource
  name: 'APIView'
  properties: {
    resource: {
      id: 'APIView'
    }
  }
}

resource databaseAccounts_apiviewuitest_name_APIViewV2 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_resource
  name: 'APIViewV2'
  properties: {
    resource: {
      id: 'APIViewV2'
    }
  }
}

resource databaseAccounts_apiviewuitest_name_00000000_0000_0000_0000_000000000001 'Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_resource
  name: '00000000-0000-0000-0000-000000000001'
  properties: {
    roleName: 'Cosmos DB Built-in Data Reader'
    type: 'BuiltInRole'
    assignableScopes: [
      databaseAccounts_apiviewuitest_name_resource.id
    ]
    permissions: [
      {
        dataActions: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/executeQuery'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/readChangeFeed'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/items/read'
        ]
        notDataActions: []
      }
    ]
  }
}

resource databaseAccounts_apiviewuitest_name_00000000_0000_0000_0000_000000000002 'Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_resource
  name: '00000000-0000-0000-0000-000000000002'
  properties: {
    roleName: 'Cosmos DB Built-in Data Contributor'
    type: 'BuiltInRole'
    assignableScopes: [
      databaseAccounts_apiviewuitest_name_resource.id
    ]
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

resource Microsoft_DocumentDB_databaseAccounts_tableRoleDefinitions_databaseAccounts_apiviewuitest_name_00000000_0000_0000_0000_000000000001 'Microsoft.DocumentDB/databaseAccounts/tableRoleDefinitions@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_resource
  name: '00000000-0000-0000-0000-000000000001'
  properties: {
    roleName: 'Cosmos DB Built-in Data Reader'
    type: 'BuiltInRole'
    assignableScopes: [
      databaseAccounts_apiviewuitest_name_resource.id
    ]
    permissions: [
      {
        dataActions: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/executeQuery'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/readChangeFeed'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/entities/read'
        ]
        notDataActions: []
      }
    ]
  }
}

resource Microsoft_DocumentDB_databaseAccounts_tableRoleDefinitions_databaseAccounts_apiviewuitest_name_00000000_0000_0000_0000_000000000002 'Microsoft.DocumentDB/databaseAccounts/tableRoleDefinitions@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_resource
  name: '00000000-0000-0000-0000-000000000002'
  properties: {
    roleName: 'Cosmos DB Built-in Data Contributor'
    type: 'BuiltInRole'
    assignableScopes: [
      databaseAccounts_apiviewuitest_name_resource.id
    ]
    permissions: [
      {
        dataActions: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/tables/*'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/*'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/entities/*'
        ]
        notDataActions: []
      }
    ]
  }
}

resource storageAccounts_apiviewuitest_name_default 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' = {
  parent: storageAccounts_apiviewuitest_name_resource
  name: 'default'
  sku: {
    name: 'Standard_LRS'
    tier: 'Standard'
  }
  properties: {
    lastAccessTimeTrackingPolicy: {
      enable: true
      name: 'AccessTimeTracking'
      trackingGranularityInDays: 1
      blobType: [
        'blockBlob'
      ]
    }
    changeFeed: {
      enabled: false
    }
    restorePolicy: {
      enabled: false
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
    cors: {
      corsRules: []
    }
    deleteRetentionPolicy: {
      allowPermanentDelete: false
      enabled: true
      days: 7
    }
    isVersioningEnabled: false
  }
}

resource Microsoft_Storage_storageAccounts_fileServices_storageAccounts_apiviewuitest_name_default 'Microsoft.Storage/storageAccounts/fileServices@2024-01-01' = {
  parent: storageAccounts_apiviewuitest_name_resource
  name: 'default'
  sku: {
    name: 'Standard_LRS'
    tier: 'Standard'
  }
  properties: {
    protocolSettings: {
      smb: {}
    }
    cors: {
      corsRules: []
    }
    shareDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

resource Microsoft_Storage_storageAccounts_queueServices_storageAccounts_apiviewuitest_name_default 'Microsoft.Storage/storageAccounts/queueServices@2024-01-01' = {
  parent: storageAccounts_apiviewuitest_name_resource
  name: 'default'
  properties: {
    cors: {
      corsRules: []
    }
  }
}

resource Microsoft_Storage_storageAccounts_tableServices_storageAccounts_apiviewuitest_name_default 'Microsoft.Storage/storageAccounts/tableServices@2024-01-01' = {
  parent: storageAccounts_apiviewuitest_name_resource
  name: 'default'
  properties: {
    cors: {
      corsRules: []
    }
  }
}

resource databaseAccounts_apiviewuitest_name_APIView_AIComments 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIView
  name: 'AIComments'
  properties: {
    resource: {
      id: 'AIComments'
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
          '/id'
        ]
        kind: 'Hash'
        version: 2
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIViewV2_APIRevisions 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIViewV2
  name: 'APIRevisions'
  properties: {
    resource: {
      id: 'APIRevisions'
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
          '/ReviewId'
        ]
        kind: 'Hash'
        version: 2
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
      computedProperties: []
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIView_Comments 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIView
  name: 'Comments'
  properties: {
    resource: {
      id: 'Comments'
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
          '/ReviewId'
        ]
        kind: 'Hash'
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIViewV2_Comments 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIViewV2
  name: 'Comments'
  properties: {
    resource: {
      id: 'Comments'
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
          '/ReviewId'
        ]
        kind: 'Hash'
        version: 2
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
      computedProperties: []
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIView_CopilotComments 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIView
  name: 'CopilotComments'
  properties: {
    resource: {
      id: 'CopilotComments'
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
          '/language'
        ]
        kind: 'Hash'
        version: 2
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIView_Profiles 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIView
  name: 'Profiles'
  properties: {
    resource: {
      id: 'Profiles'
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
          '/id'
        ]
        kind: 'Hash'
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIView_PullRequests 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIView
  name: 'PullRequests'
  properties: {
    resource: {
      id: 'PullRequests'
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
          '/PullRequestNumber'
        ]
        kind: 'Hash'
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIViewV2_PullRequests 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIViewV2
  name: 'PullRequests'
  properties: {
    resource: {
      id: 'PullRequests'
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
          '/ReviewId'
        ]
        kind: 'Hash'
        version: 2
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
      computedProperties: []
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIView_Reviews 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIView
  name: 'Reviews'
  properties: {
    resource: {
      id: 'Reviews'
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
          {
            path: '/cp_NumberOfRevisions/?'
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
          '/id'
        ]
        kind: 'Hash'
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
      computedProperties: [
        {
          name: 'cp_NumberOfRevisions'
          query: 'SELECT VALUE ARRAY_LENGTH(r.Revisions) FROM r'
        }
      ]
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIViewV2_Reviews 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIViewV2
  name: 'Reviews'
  properties: {
    resource: {
      id: 'Reviews'
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
          '/id'
        ]
        kind: 'Hash'
        version: 2
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
      computedProperties: []
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIViewV2_SamplesRevisions 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIViewV2
  name: 'SamplesRevisions'
  properties: {
    resource: {
      id: 'SamplesRevisions'
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
          '/ReviewId'
        ]
        kind: 'Hash'
        version: 2
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
      computedProperties: []
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIView_UsageSamples 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIView
  name: 'UsageSamples'
  properties: {
    resource: {
      id: 'UsageSamples'
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
          '/ReviewId'
        ]
        kind: 'Hash'
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIView_default 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/throughputSettings@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIView
  name: 'default'
  properties: {
    resource: {
      throughput: 400
      autoscaleSettings: {
        maxThroughput: 4000
      }
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_3d33b5c3_a6b1_4a5e_85f1_bc9782557851 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_resource
  name: '3d33b5c3-a6b1-4a5e-85f1-bc9782557851'
  properties: {
    roleDefinitionId: databaseAccounts_apiviewuitest_name_00000000_0000_0000_0000_000000000002.id
    principalId: 'f0552653-da7c-4e30-9ddd-d7270d56ead3'
    scope: databaseAccounts_apiviewuitest_name_resource.id
  }
}

resource databaseAccounts_apiviewuitest_name_5d93eb31_22d2_4905_96ff_733be16ee3b0 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_resource
  name: '5d93eb31-22d2-4905-96ff-733be16ee3b0'
  properties: {
    roleDefinitionId: databaseAccounts_apiviewuitest_name_00000000_0000_0000_0000_000000000002.id
    principalId: 'cc92b91b-7978-4752-9d1c-bed62e9de24f'
    scope: databaseAccounts_apiviewuitest_name_resource.id
  }
}

resource databaseAccounts_apiviewuitest_name_744bf038_8dd4_4e19_ab27_62972e145636 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_resource
  name: '744bf038-8dd4-4e19-ab27-62972e145636'
  properties: {
    roleDefinitionId: databaseAccounts_apiviewuitest_name_00000000_0000_0000_0000_000000000002.id
    principalId: 'c3bfd883-d625-45f8-be02-82432b31a062'
    scope: databaseAccounts_apiviewuitest_name_resource.id
  }
}

resource databaseAccounts_apiviewuitest_name_a6ed4cc6_7784_4013_b484_d2c99a843307 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_resource
  name: 'a6ed4cc6-7784-4013-b484-d2c99a843307'
  properties: {
    roleDefinitionId: databaseAccounts_apiviewuitest_name_00000000_0000_0000_0000_000000000002.id
    principalId: 'bcb0cf5a-9d34-4ae2-8e9d-c0302c9e7902'
    scope: databaseAccounts_apiviewuitest_name_resource.id
  }
}

resource Microsoft_DocumentDB_databaseAccounts_tableRoleAssignments_databaseAccounts_apiviewuitest_name_3d33b5c3_a6b1_4a5e_85f1_bc9782557851 'Microsoft.DocumentDB/databaseAccounts/tableRoleAssignments@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_resource
  name: '3d33b5c3-a6b1-4a5e-85f1-bc9782557851'
  properties: {
    roleDefinitionId: Microsoft_DocumentDB_databaseAccounts_tableRoleDefinitions_databaseAccounts_apiviewuitest_name_00000000_0000_0000_0000_000000000002.id
    principalId: 'f0552653-da7c-4e30-9ddd-d7270d56ead3'
    scope: databaseAccounts_apiviewuitest_name_resource.id
  }
}

resource Microsoft_DocumentDB_databaseAccounts_tableRoleAssignments_databaseAccounts_apiviewuitest_name_5d93eb31_22d2_4905_96ff_733be16ee3b0 'Microsoft.DocumentDB/databaseAccounts/tableRoleAssignments@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_resource
  name: '5d93eb31-22d2-4905-96ff-733be16ee3b0'
  properties: {
    roleDefinitionId: Microsoft_DocumentDB_databaseAccounts_tableRoleDefinitions_databaseAccounts_apiviewuitest_name_00000000_0000_0000_0000_000000000002.id
    principalId: 'cc92b91b-7978-4752-9d1c-bed62e9de24f'
    scope: databaseAccounts_apiviewuitest_name_resource.id
  }
}

resource Microsoft_DocumentDB_databaseAccounts_tableRoleAssignments_databaseAccounts_apiviewuitest_name_744bf038_8dd4_4e19_ab27_62972e145636 'Microsoft.DocumentDB/databaseAccounts/tableRoleAssignments@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_resource
  name: '744bf038-8dd4-4e19-ab27-62972e145636'
  properties: {
    roleDefinitionId: Microsoft_DocumentDB_databaseAccounts_tableRoleDefinitions_databaseAccounts_apiviewuitest_name_00000000_0000_0000_0000_000000000002.id
    principalId: 'c3bfd883-d625-45f8-be02-82432b31a062'
    scope: databaseAccounts_apiviewuitest_name_resource.id
  }
}

resource Microsoft_DocumentDB_databaseAccounts_tableRoleAssignments_databaseAccounts_apiviewuitest_name_a6ed4cc6_7784_4013_b484_d2c99a843307 'Microsoft.DocumentDB/databaseAccounts/tableRoleAssignments@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_resource
  name: 'a6ed4cc6-7784-4013-b484-d2c99a843307'
  properties: {
    roleDefinitionId: Microsoft_DocumentDB_databaseAccounts_tableRoleDefinitions_databaseAccounts_apiviewuitest_name_00000000_0000_0000_0000_000000000002.id
    principalId: 'bcb0cf5a-9d34-4ae2-8e9d-c0302c9e7902'
    scope: databaseAccounts_apiviewuitest_name_resource.id
  }
}

resource storageAccounts_apiviewuitest_name_default_codefiles 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: storageAccounts_apiviewuitest_name_default
  name: 'codefiles'
  properties: {
    immutableStorageWithVersioning: {
      enabled: false
    }
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'None'
  }
  dependsOn: [
    storageAccounts_apiviewuitest_name_resource
  ]
}

resource storageAccounts_apiviewuitest_name_default_comments 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: storageAccounts_apiviewuitest_name_default
  name: 'comments'
  properties: {
    immutableStorageWithVersioning: {
      enabled: false
    }
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'None'
  }
  dependsOn: [
    storageAccounts_apiviewuitest_name_resource
  ]
}

resource storageAccounts_apiviewuitest_name_default_originals 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: storageAccounts_apiviewuitest_name_default
  name: 'originals'
  properties: {
    immutableStorageWithVersioning: {
      enabled: false
    }
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'None'
  }
  dependsOn: [
    storageAccounts_apiviewuitest_name_resource
  ]
}

resource storageAccounts_apiviewuitest_name_default_testingdata 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: storageAccounts_apiviewuitest_name_default
  name: 'testingdata'
  properties: {
    immutableStorageWithVersioning: {
      enabled: false
    }
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'None'
  }
  dependsOn: [
    storageAccounts_apiviewuitest_name_resource
  ]
}

resource storageAccounts_apiviewuitest_name_default_testingdatapublic 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: storageAccounts_apiviewuitest_name_default
  name: 'testingdatapublic'
  properties: {
    immutableStorageWithVersioning: {
      enabled: false
    }
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'None'
  }
  dependsOn: [
    storageAccounts_apiviewuitest_name_resource
  ]
}

resource storageAccounts_apiviewuitest_name_default_usagesamples 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: storageAccounts_apiviewuitest_name_default
  name: 'usagesamples'
  properties: {
    immutableStorageWithVersioning: {
      enabled: false
    }
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'None'
  }
  dependsOn: [
    storageAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIViewV2_APIRevisions_default 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/throughputSettings@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIViewV2_APIRevisions
  name: 'default'
  properties: {
    resource: {
      throughput: 400
      autoscaleSettings: {
        maxThroughput: 4000
      }
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_APIViewV2
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIViewV2_Comments_default 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/throughputSettings@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIViewV2_Comments
  name: 'default'
  properties: {
    resource: {
      throughput: 400
      autoscaleSettings: {
        maxThroughput: 4000
      }
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_APIViewV2
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIViewV2_PullRequests_default 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/throughputSettings@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIViewV2_PullRequests
  name: 'default'
  properties: {
    resource: {
      throughput: 400
      autoscaleSettings: {
        maxThroughput: 4000
      }
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_APIViewV2
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIViewV2_Reviews_default 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/throughputSettings@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIViewV2_Reviews
  name: 'default'
  properties: {
    resource: {
      throughput: 400
      autoscaleSettings: {
        maxThroughput: 4000
      }
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_APIViewV2
    databaseAccounts_apiviewuitest_name_resource
  ]
}

resource databaseAccounts_apiviewuitest_name_APIViewV2_SamplesRevisions_default 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/throughputSettings@2024-12-01-preview' = {
  parent: databaseAccounts_apiviewuitest_name_APIViewV2_SamplesRevisions
  name: 'default'
  properties: {
    resource: {
      throughput: 400
      autoscaleSettings: {
        maxThroughput: 4000
      }
    }
  }
  dependsOn: [
    databaseAccounts_apiviewuitest_name_APIViewV2
    databaseAccounts_apiviewuitest_name_resource
  ]
}
