
# Codeowners-Utils

Codeowners Utils contains utilities to both parse and lint CODEOWNERS files in Azure Sdk repositories. The reason why this is necessary, instead just using GitHub's CODEOWNERS validation, is that we have our own type of metadata, which exists in comments in the CODEOWNERS files, which is something that requires additional validation. [Metadata - definition, usage and block structure can be found here](./METADATA.md)

## Parsing

The entry points for parsing and matching paths against CODEOWNERS entries exist in [CodeownersParser](./Azure.Sdk.Tools.CodeownersUtils/Parsing/CodeownersParser.cs). The CODEOWNERS data is parsed into a list of[CodeownersEntry](./Azure.Sdk.Tools.CodeownersUtils/Parsing/CodeownersEntry.cs). ParseCodeownersFile requires the file or URL of the CODEOWNERS file to parse and takes an optional URI to override the default team/user data URI which is used to expand teams in CODEOWNERS entries. The resulting list entries are in the same order as they are in the CODEOWNERS file.

 ```csharp
 List<CodeownersEntry> codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, [teamStorageURI]);
 ```

The resulting list of CodeownersEntry objects are typically used to get the matching CodeownersEntry for a given repository file. There are several tools that use this information today like github-event-processor, notification-configuration, and pipeline-owners-extractor. Like GitHub, it matches things in reverse order, meaning that it starts from the end of the list and returns the first match that it finds or an empty entry if there was no match.

```csharp
string buildDefPath = "tools/github-event-processor/ci.yml";
CodeownersEntry codeownersEntry = CodeownersParser.GetMatchingCodeownersEntry(buildDefPath, codeownersEntries);
```

## CodeownersEntry object

A CodeownersEntry object has the following members:

- **PathExpression** - If the CODEOWNERS metadata block ended in a source path/owner line, this is the path portion. For example, `sdk/ServiceDirectory1` or `/sdk/**/azure-myservice-*/` etc. This is empty if the meta
- **SourceOwners** - The list of owners for a given source path/owner line. This is empty if there are no owners or, the metadata block did not end in a source path/owner line.
- **PRLabels** - The list of labels parsed from the `PRLabel` metadata moniker or empty if metadata block did not contain the moniker.
- **AzureSdkOwners** - The list of owners parsed from the `AzureSdkOwner` metadata moniker, empty if metadata block did not contain the moniker or, if the moniker had no users defined but was part of a block that ended in a source path/owners line, this list would contain the same owners as the `SourceOwners`.
- **ServiceLabels** - The list of labels parsed from the `ServiceLabel` metadata moniker or empty if metadata block did not contain the moniker.
- **ServiceOwners** - The list of owners parsed from the `ServiceOwner` metadata moniker. Empty if metadata block did not contain the moniker or, if the ServiceLabel is part of a block that ends in a source path/owner line, these owners will be the same as the `SourceOwners`.

Additional note about owners and parsing: The team/user data is used to expand teams into a list of users but in order to do so, the team needs to be a child team of azure-sdk-write. The reasoning for this is twofold, the first being that the GitHub criteria for an owner in a CODEOWNERS file is that the owner must have write permission with the second being that all teams with write permissions for our repositories are children of azure-sdk-write. Similarly, in order to have write permission for Azure/azure-sdk* repositories, individual owners must be direct members of azure-sdk-write or one of its child teams.

## Linting

This tool will analyze an azure-sdk* CODEOWNERS file, including our specific brand of Metadata, to ensure correctness. It'll output errors in metadata blocks as well as errors for any individual lines within a metadata block.

### What is verified during linting?

- **Owners**
  - Users and Teams are verified to have write permissions.
  - Users are also verified to be Public members of Azure. This documented in the [azure-sdk onboarding docs for acess](https://eng.ms/docs/products/azure-developer-experience/onboard/access). This is necessary for tooling in order to be able to determine Azure org membership for a given user. This cannot be done if the user's Azure membership is private and the tooling will process them as if they weren't a member of Azure.
  - Malformed team entries, entries missing the prepended org `@Azure/` can be detected but only if they're child teams of azure-sdk-write.
- **Labels**
  - Whether or not the label exists in a particular repository.
- **Source paths** (not something GitHub verifies)
  - Does the path (directory or file) exist?
  - If the path is a glob
    - Is the glob syntactically valid? Does it contain [forbidden characters](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners#codeowners-syntax) or is it malformed?
    - Does the glob have any matches in the repository?
- **Metadata block formatting**
  - Verifies the correctness of metadata blocks according our [Metadata definitions](./METADATA.md).

### Errors

There are two types of errors, single line errors and block formatting errors.

- **Single line error** - These verification errors for a single CODEOWNERS line. The error message will contain the line number, the source line and all of the errors. For example:

```text
Error(s) on line 125
Source Line: /sdk/SomeServiceDirectory/BadPath                        @fakeUser1 @FakeTeam @fakeUser2
  -/sdk/SomeServiceDirectory/BadPath path or file does not exist in repository.
  -fakeUser1 is not a public member of Azure.
  -FakeTeam is a malformed team entry and should start with '@Azure/'.
  -fakeUser2 is an invalid user. Ensure the user exists, is public member of Azure and has write permissions.
```

- **Block formatting error** - These are errors with the formatting of the metadata block. The error message will clearly identify that it's a block error and will contain the start/end line numbers as well as the contents of the block. In the example below, the ServiceLabel entry would be associated with the source path/owners line if it weren't commented out. A ServiceLabel needs owners.

```text
Source block error.
Source Block Start: 728
  # ServiceLabel: %FakeLabel1 %Service Attention
  #/sdk/SomeServiceDirectory/                                              @fakeUser3
Source Block End: 729
  -ServiceLabel needs to be followed by, /<NotInRepo>/ or ServiceOwners with owners, or a source path/owner line.
```

### Where will this run?

Right now, plan is to have this running in same set of repositories that the github-event-processor runs in it's this set of repositories that utilize CODEOWNERS data along with our flavor of metadata. There will be a nightly pipeline run but the pipeline will also run on pull requests with CODEOWNERS changes. _Note: GitHub's linting might say a particular CODEOWNERS file is clean, that's not necessarily true with our metadata._

#### What about the existing errors?

Every repository that this will be enabled for will have a baseline file, `CODEOWNERS_baseline_errors.txt`, sitting next to its CODEOWNERS file if there are existing errors. This file is a deduplicated list of known single line errors, without the line and line numbers, and can be used by the linter to filter its output. This will prevent new errors from getting into the file as well as allow the nightly pipeline runs to start out passing. The downside of this is that if someone adds a new line that contains an existing error it wouldn't get caught. The repository owners will ultimately be responsible for cleaning out these errors and when they're all cleaned up, the file is removed. To the linter, no baseline file means no existing errors.

#### Why only filter Single errors without the line and line number?

Line numbers become moot as soon as someone adds a line to the CODEOWNERS file and there's really no good way deal with this. Further, single line errors contain errors that pertain to an invalid path, invalid owners, malformed team names, invalid labels etc. and these are type of errors that can exist multiple times in a file. A path should only exist once in a CODEOWNERS file but owners and labels can exist multiple times. For example, if @owner1 isn't a public member of Azure and exists as an owner for multiple items all of those errors are filtered out with the 1 deduplicated line. Block formatting errors, on the other hand, are errors that are very specific to a given block and if parsing encounters a block with errors, that entry is thrown out. If block formatting errors were deduplicated, someone adding a new block could copy and paste from a block with formatting errors. Because errors are deduplicated, the pipeline running for the CODEOWNERS changes wouldn't report the block error and something down the line using the parser, wouldn't work as expected because the block was bad and there's no parsed entry for it. The exiting repositories, that the linter will be running in, have no CODEOWNERS block errors and this will prevent them from being added.

### Where does the team/user and label data come from

The team/user, org visibility and repository label data are stored in Azure Blob Storage. The data is populated by the [github-team-user-store](https://github.com/Azure/azure-sdk-tools/tree/main/tools/github-team-user-store) which runs daily as part of the pipeline-owners-extractor. _Note: To be very clear, this information is not secret nor is it exposing anything that can't already be gleaned from the existing CODEOWNERS files or, in the case of labels, inspecting the repository._ This has to be done this way for the following reasons:

- Fetching this information requires a specific GitHub token with specific permissions which cannot be granted GitHub workflows.
- This is going to run as part of a nightly run as well as a CI pipeline when changes to CODEOWNERS files are made and public pipelines cannot have variables.

This also means that certain changes, like a user changing their Azure membership to public or a new label was added to the repository or a new team was added, won't be immediately picked up. The pipeline-owners-extractor would need to run so the pre-populated data reflects them.

### Codeowners Utils has following requirements and dependencies

1. **Linter only** Must be run in a full repository, not a sparse checked out one. The reason for this is that paths in the CODEOWNERS are verified to exist and, if they're a glob path, they actually have matches in the repository.
2. Microsoft.Extensions.FileSystemGlobbing. This is used by the linter to verify the paths in CODEOWNERS exist or, if globs, they have matches. Used by parser's GetMatchingCodeownersEntry to determine whether or not a CodeownersEntry matches the target path of what's passed in.

In addition to the above dependencies, the Azure.Sdk.Tools.CodeownersUtils.Tests project has the following additional test dependencies.

1. NUnit - NUnit unit testing
2. NUnit3TestAdapter - Used to run NUnit3 tests in Visual Studio
3. Microsoft.NET.Test.Sdk - For the ability to run tests using **dotnet test**
4. coverlet.collector - Generates test coverage data
