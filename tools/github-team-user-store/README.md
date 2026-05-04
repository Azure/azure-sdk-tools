# GitHub Team User Store

## Overview

The github-team-user-store is an internal only tool. The tool traverses the Azure/azure-sdk-write team hierarchy through the Open Source Portal (OSP), gets the member lists for each team, and writes the resulting cache files locally. The purpose of this is to enable team usage in CODEOWNERS by allowing the CodeOwnersParser to pull the list of users when it encounters a non-user, or team, entry. The reason this code is not directly in the CodeOwnersParser is because it requires Open Source API access that is not readily available or prudent to add to every consumer.

### github-team-user-store processing

The tool creates:
- `azure-sdk-write-teams-blob`: a `List<KeyValuePair<string, List<string>>>` encoded as JSON where the key is the team name and the value is the list of user logins
- `user-org-visibility-blob`: a `Dictionary<string, bool>` encoded as JSON for the `azure-sdk-write` users, where the value is `true` when the user's Azure org membership is public
- `repository-labels-blob`: a `Dictionary<string, HashSet<string>>` encoded as JSON keyed by full repository name

OSP supplies the team hierarchy, team members, public Azure org membership, and repository labels. Cache generation no longer depends on GitHub.

The tool writes these files to the directory provided by `--outputDirectory`. A separate step can upload those files.

### Tool requirements

The tool requires one thing:

1. An Azure credential that can call the Open Source API endpoints used for team children, team members, public memberships, and repository labels. The tool uses Azure Identity to acquire that token.

### Command line

```powershell
dotnet run -- --outputDirectory "<cache-output-dir>" --repositoryListFile "<path-to-repositories.txt>"
```

This writes the following files to `<cache-output-dir>`:
- `azure-sdk-write-teams-blob`
- `user-org-visibility-blob`
- `repository-labels-blob`

### Using the store data

The team/user json blob is created from the dictionary but, because the serializer does not handle `Dictionary<string, List<string>>` in the shape expected by existing consumers, the dictionary is converted to a `List<KeyValuePair<string, List<string>>>`. Anything reading this data needs to convert it back to a dictionary, which can be done with the following C# code.

```Csharp
    var list = JsonSerializer.Deserialize<List<KeyValuePair<string, List<string>>>>(rawJson);
    var TeamUSerDictionary = list.ToDictionary((keyItem) => keyItem.Key, (valueItem) => valueItem.Value);
```

### The pipeline where this will run
This will run as part of the [automation - pipeline-owners-extraction
](https://dev.azure.com/azure-sdk/internal/_build?definitionId=5112&_a=summary) pipeline. The tool generates local cache files, and the pipeline publishes that directory as an artifact so a separate step can upload the files.
