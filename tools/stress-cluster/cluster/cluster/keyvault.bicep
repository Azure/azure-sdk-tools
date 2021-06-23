param tags object = {}
param location string = resourceGroup().location
param keyVaultName string
param tenantId string = subscription().tenantId
param objectIds array

@secure()
param secretsObject object

param secretsPermissions array = [
    'list'
    'get'
    'set'
]

@allowed([
    'standard'
    'premium'
])
param skuName string = 'standard'

resource vault 'Microsoft.KeyVault/vaults@2019-09-01' = {
    name: keyVaultName
    location: location
    tags: tags
    properties: {
        tenantId: tenantId
        accessPolicies: [for objectId in objectIds: {
            objectId: objectId
            tenantId: tenantId
            permissions: {
                secrets: secretsPermissions
            }
        }]
        sku: {
            name: skuName
            family: 'A'
        }
        networkAcls: {
            defaultAction: 'Allow'
            bypass: 'AzureServices'
        }
    }
}

resource secrets 'Microsoft.KeyVault/vaults/secrets@2018-02-14' = [for secret in secretsObject.secrets: {
    name: '${vault.name}/${secret.secretName}'
    properties: {
        value: secret.secretValue
    }
}]
