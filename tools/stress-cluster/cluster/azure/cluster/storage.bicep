param location string = resourceGroup().location
param storageName string
param fileShareName string

resource storage 'Microsoft.Storage/storageAccounts@2019-06-01' = {
  name: storageName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
}

resource fileshare 'Microsoft.Storage/storageAccounts/fileServices/shares@2021-04-01' = {
      name: '${storage.name}/default/${fileShareName}'
      properties: { }
}

output name string = storage.name
output key string = storage.listKeys().keys[0].value
output fileShareName string = fileShareName
