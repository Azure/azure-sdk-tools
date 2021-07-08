param vaultName string
param objectId string
param tenantId string

// Add cluster node identity to statically configured stress test secrets keyvault
resource stressTestVault 'Microsoft.KeyVault/vaults/accessPolicies@2019-09-01' = {
    name: '${vaultName}/add'
    properties: {
        accessPolicies: [
            {
                objectId: objectId
                tenantId: tenantId
                permissions: {
                    secrets: [
                        'list'
                        'get'
                    ]
                }
            }
        ]
    }
}
