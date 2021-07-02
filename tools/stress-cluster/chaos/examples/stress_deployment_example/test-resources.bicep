targetScope = 'subscription'

param groupName string
param location string
param now string = utcNow('u')

resource group 'Microsoft.Resources/resourceGroups@2020-10-01' = {
    name: 'rg-${groupName}-${uniqueString(now)}'
    location: location
    tags: {
        DeleteAfter: dateTimeAdd(now, 'PT8H')
    }
}

output RESOURCE_GROUP string = group.name
