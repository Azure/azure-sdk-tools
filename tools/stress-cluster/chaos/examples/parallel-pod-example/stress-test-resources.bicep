// Unique short string safe for naming resources like storage, service bus.
param BaseName string = ''

resource config 'Microsoft.AppConfiguration/configurationStores@2020-07-01-preview' = {
  name: 'stress-${BaseName}'
  location: resourceGroup().location
  sku: {
    name: 'Standard'
  }
}

output RESOURCE_GROUP string = resourceGroup().name
output APP_CONFIG_NAME string = config.name
