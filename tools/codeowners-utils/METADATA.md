# CODEOWNERS Metadata

Azure-sdk* repository CODEOWNERS files contain data beyond the normal source path/owner lines. This metadata is used by workflows for processing. The most notable consumer is [github-event-processor](https://github.com/Azure/azure-sdk-tools/tree/main/tools/github-event-processor) which uses these when processing certain GitHub Actions. The full set of action processing rules is documented [here](https://github.com/Azure/azure-sdk-tools/blob/main/tools/github-event-processor/RULES.md). The common definitions used in this document are as follows:

- **Moniker** - Any of the existing tags we currently support. PRLabel or ServiceLabel would be examples of this.
- **Metadata block** - A block containing one or more metadata tags and/or a source path/owner line. Blocks are  surrounded by blank lines. A block can contain comment lines.
  - **Source path/owner line** - A single source path/owner line in CODEOWNERS is its own block. There are some metadata tags that must be part of a block that ends in source path/owner line and some tags that can be. These are defined below.

## Metadata Monikers

- **AzureSdkOwners:** - This moniker is used to denote Azure Sdk owners that have triage responsibility for a given source path where the people responsible for triage aren't the same people responsible for pull request reviews. This moniker must be part of a block that ends in a source path/owner line.
- **PRLabel:** - This moniker is used by workflows to determine what label(s) will get added to a pull request based upon the file paths of the files in the pull request. This moniker must be part of a block that ends in a source path/owner line.
- **ServiceLabel:** - This moniker contains the service label that's used to figure out what users need to be @ mentioned in an issue when the Service Attention label is added. This moniker must be part of a block that either ends in a source path/owner line, the ServiceOwners moniker or the /&lt;NotInRepo&gt;/ moniker. If the ServiceLabel is part of a block that ends in the source path/owner line, the service owners are inferred from that.
- **ServiceOwners:** - The moniker is used to identify the owners associated with a service label if the service label isn't part of a block ending in a source path/owner line. This moniker cannot be part of a source path/owner line.
- **/&lt;NotInRepo&gt;/** - This is the existing moniker used to denote service owners in CODEOWNERS files. This will ultimately be replaced by the ServiceOwners moniker, which more clearly defines what it actually is, but right now the parser and linter will handle both. This moniker cannot be part of a source path/owner line. Also, this is the only moniker that doesn't have a colon separator after the moniker, before the labels.

## Examples of moniker usage in CODEOWNERS files

This list of examples is exhaustive. If an example isn't in here then it won't work and the linter will catch it.

- A single source path/owner line is its own block.

```text

# Optional comment
/sdk/SomePath/  @fakeUser1 @fakeUser2 @Azure/fakeTeam1

```

- `AzureSdkOwners` must be part of a block that ends in a source path/owner. If the moniker's owners are blank then they will be set to the same owners as the source owners. In the example below, AzureSdkOwners:, left empty would take @fakeUser1 @fakeUser2 @Azure/fakeTeam1 from the source owners.

```text

# AzureSdkOwners: %fakeUser3 %fakeUser4
/sdk/SomePath/  @fakeUser1 @fakeUser2 @Azure/fakeTeam1
OR
# AzureSdkOwners:
/sdk/SomePath/  @fakeUser1 @fakeUser2 @Azure/fakeTeam1

```

- `PRLabel` must be part of a block that ends in a source path/owner

```text

# PRLabel: %Label1 %Label2
/sdk/SomePath/  @fakeUser1 @fakeUser2 @Azure/fakeTeam1

```

- If a `ServiceLabel` is part of a block that ends in a source path/owner line, the ServiceOwners will be the inferred to be the same as the source owners. _This is time that a moniker will have inferred data._

```text

# ServiceLabel: %Label1 %Label2
/sdk/SomePath/  @fakeUser1 @fakeUser2 @Azure/fakeTeam1

```

- If a `ServiceLabel` is not part of a block that ends in a source path/owner line, then it must be part of a two line block consisting only of a ServiceLabel and either a ServiceOwners or /&lt;NotInRepo&gt;/. New entries should use ServiceOwners.

```text

# ServiceLabel: %Label1 %Label2
# ServiceOwners: @fakeUser1 @Azure/fakeTeam1
OR
# ServiceLabel: %Label1 %Label2
# /&lt;NotInRepo&gt;/ @fakeUser1 @Azure/fakeTeam1

```

- Everything that can be part of a source path/owners block. ServiceOwners will be inferred from the path owners. AzureSdkOwners, if empty, would do the same.

```text

# AzureSdkOwners: %fakeUser3 %fakeUser4
# ServiceLabel: %Label1 %Label2
# PRLabel: %Label3
/sdk/SomePath/  @fakeUser1 @fakeUser2 @Azure/fakeTeam1

```
