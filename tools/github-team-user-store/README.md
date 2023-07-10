# GitHub Team User Store

## Overview

The github-team-user-store is an internal only tool. The tool will recursively get the list of teams/users, that includes the Azure/azure-sdk-write and all of the teams within that hierarchy, and store the results as a json string in Azure blob storage. The purpose of this is to enable team usage in CODEOWNERS by allowing the CodeOwnersParser to pull the list of users when it encounters a non-user, or team, entry. The reason why this code isn't directly in the CodeOwnersParser is because it requires a specific, fine-grained Personal Access Token (PAT) in order to be able to get the users for teams which isn't something that's readily available or prudent to be added to all of the places where CodeOwnersParser is used.

### github-team-user-store processing

The tool uses creates a Dictionary<string, List`<string`>> where the key is the team name and the value is list of users, specifically the user Logins. The Logins are what's used by GitHub in @mentions, assignments etc. The specific details about the API calls can be found in code.

### Tool requirements

The tool requires two things:

1. A GitHub PAT from a user that has organization access in the GITHUB_TOKEN environment variable. This requires a GitHub fine-grained token with Organization->Membership read-only permission. A bot account won't work here, this needs to be created by a user with permissions for the org. Note: Nothing is edited here, only read.

2. The SAS token with write permissions for the azure-sdk-write-teams container in the azuresdkartifacts storage account in the AZURE_SDK_TEAM_USER_STORE_SAS environment variable.

Both the PAT and the SAS token are in the **azuresdkartifacts azure-sdk-write-teams variables** variable group to be used by the pipeline which will run this on, at least, a daily basis.

### Using the store data

The json blob is read anonymously. The json blob is created from the dictionary but, because the serializer doesn't handle Dictionary<string, List`<string`>>, the dictionary is converted to a List<KeyValuePair<string, List`<string`>>>. This means that anything wanting to use this data needs to convert it back to a dictionary which is easily done with the following C# code.

```Csharp
    var list = JsonSerializer.Deserialize<List<KeyValuePair<string, List<string>>>>(rawJson);
    var TeamUSerDictionary = list.ToDictionary((keyItem) => keyItem.Key, (valueItem) => valueItem.Value);
```

### The pipeline where this will run
This will run as part of the [pipeline-owners-extracton](https://dev.azure.com/azure-sdk/internal/_build?definitionId=5112&_a=summary) pipeline.