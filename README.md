# Azure SDK Tools

This repository contains useful tools that the Azure SDK team utilizes across their infrastructure.

## Index

| Package or Intent              | Path                                                    | Description                                                                          | Status                                                                                                                                                                                                                                     |
| ------------------------------ | ------------------------------------------------------- | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| doc-warden                     | [Readme](packages/python-packages/doc-warden/README.md) | A tool used to enforce readme standards across Azure SDK Repos.                      | [![Build Status](https://dev.azure.com/azure-sdk/public/_apis/build/status/108?branchName=master)](https://dev.azure.com/azure-sdk/public/_build/latest?definitionId=108&branchName=master)                                                |
| pixel-server                   | [Readme](web/pixel-server/README.md)                   | A tiny ASP.NET Core site used to serve a pixel and record impressions.               | Not Yet Enabled                                                                                                                                                                                                                            |
| pixel insertion tool           | [Readme](scripts/python/readme_tracking/readme.md)      | A tool used to insert the requests for images served by `pixel server`.              | Not Yet Enabled                                                                                                                                                                                                                            |
| Check Enforcer                 | [Readme](tools/check-enforcer/README.md)               | Manage GitHub check-runs in a mono-repo.                                             | Not Yet Enabled                                                                                                                                                                                                                            |
| Maven Plugin for Snippets      | [Readme](packages/java-packages/snippet-replacer-maven-plugin/README.md)               | A Maven plugin that that updates code snippets referenced from javadoc comments.                                             | Not Yet Enabled                                                                                                                                                                                                                            |

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit <https://cla.microsoft.com>.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

[![Azure DevOps builds](https://img.shields.io/azure-devops/build/azure-sdk/internal/1372?label=eng%2Fcommon%20sync)](https://dev.azure.com/azure-sdk/internal/_build/latest?definitionId=1372&branchName=master)

## Common Engineering System

The `eng/common` directory contains engineering files that are common across the various azure-sdk language repos.
It should remain relatively small and only contain textual based files like scripts, configs, or templates. It
should not contain binary files as they don't play well with git.

### Code Structure

To help keep the content of the `eng/common` directory as small as possible we should split the language specific parts of a common script out if possible.
- Keep the language specific logic as a function in the `eng/scripts/Language-Settings.ps1` files found in each language repo.
- Assign the function name to a variable in the `eng/common/scripts/common.ps1` file.
- Call the function using the variable name like so `&$VariableName`.

### Updating

Any updates to files in the `eng/common` directory should be made in the [azure-sdk-tools](https://github.com/azure/azure-sdk-tools) repo.
All changes made will cause a PR to created in all subscribed azure-sdk language repos which will blindly replace all contents of
the `eng/common` directory in that repo. For that reason do **NOT** make changes to files in this directory in the individual azure-sdk
languages repos as they will be overwritten the next time an update is taken from the common azure-sdk-tools repo.

### Workflow

The 'Sync eng/common directory' PRs will be created in the language repositories when a pull request that touches the eng/common directory is submitted against the master branch. This will make it easier for changes to be tested in each individual language repo before merging the changes in the azure-sdk-tools repo. The workflow is explained below:

1. Create a PR (**Tools PR**) in the `azure-sdk-tools` repo with changes to eng/common directory.
2. `azure-sdk-tools - sync - eng-common` pipeline is triggered for the **Tools PR**
3. The  `azure-sdk-tools - sync - eng-common` pipeline queues test runs for template pipelines in various languages. These help you test your changes in the **Tools PR**.
4. If there are changes in the **Tools PR** that will affect the release stage you should approve the release test pipelines by clicking the approval gate. The test (template) pipeline will automatically release the next eligible version without needing manual intervention for the versioning. Please approve your test releases as quickly as possible. A race condition may occur due to someone else queueing the pipeline and going all the way to release using your version while yours is still waiting. If this occurs manually rerun the pipeline that failed.
5.  If you make additional changes to your **Tools PR** repeat steps 1 - 4 until you have completed the necessary testing of your changes. This includes full releases of the template package, if necessary.
6. Sign off on CreateSyncPRs stage of the sync pipeline using the approval gate. This stage will create the **Sync PRs** in the various language repos. A link to each of the **Sync PRs** will show up in the **Tools PR** for you to click and review.
7. Go review and approve each of your **Sync PRs**.
8. Sign Off on the VerifyAndMerge stage. This will merge any remaining open **Sync PR** and also append `auto-merge` to the **Tools PR**.
   * If a **Sync PR** has any failing checks, it will need to be manually merged, even if `/check-enforcer override` has been run ([azure-sdk-tools#1147](https://github.com/Azure/azure-sdk-tools/issues/1147)).

C:/repo/sdk-tools/packages/java-packages/snippet-replacer-maven-plugin