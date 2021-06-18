param tags object = {}
param location string = resourceGroup().location
@secure()
param secretsObject object

@description('Specifies the name of the key vault.')
param keyVaultName string

@description('Specifies the Azure Active Directory tenant ID that should be used for authenticating requests to the key vault. Get it by using Get-AzSubscription cmdlet.')
param tenantId string = subscription().tenantId

@description('Specifies the client ID of a user, service principal or security group in the Azure Active Directory tenant for the vault. The object ID must be unique for the list of access policies. Get it by using Get-AzADUser or Get-AzADServicePrincipal cmdlets.')
param objectId string

@description('Specifies the permissions to secrets in the vault. Valid values are: all, get, list, set, delete, backup, restore, recover, and purge.')
param secretsPermissions array = [
    'list'
    'get'
    'set'
]

@allowed([
    'standard'
    'premium'
])
@description('Specifies whether the key vault is a standard vault or a premium vault.')
param skuName string = 'standard'

resource vault 'Microsoft.KeyVault/vaults@2019-09-01' = {
    name: keyVaultName
    location: location
    tags: tags
    properties: {
        tenantId: tenantId
        accessPolicies: [
            {
                objectId: objectId
                tenantId: tenantId
                permissions: {
                    secrets: secretsPermissions
                }
            }
        ]
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
