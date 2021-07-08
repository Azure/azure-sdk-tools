# Azure SDK Tools

This repository contains useful tools that the Azure SDK team utilizes across their infrastructure.

## Index

| Package or Intent              | Path                                                    | Description                                                                          | Status                                                                                                                                                                                                                                     |
| ------------------------------ | ------------------------------------------------------- | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Check Enforcer                 | [Readme](tools/check-enforcer/README.md)               | Manage GitHub check-runs in a mono-repo.                                             | Not Yet Enabled                                                                                                                                                                                                                            |
| doc-warden                     | [Readme](packages/python-packages/doc-warden/README.md) | A tool used to enforce readme standards across Azure SDK Repos.                      | [![Build Status](https://dev.azure.com/azure-sdk/public/_apis/build/status/108?branchName=main)](https://dev.azure.com/azure-sdk/public/_build/latest?definitionId=108&branchName=main)                                                |
| http-fault-injector            | [Readme](tools/http-fault-injector/README.md)           | HTTP proxy server for testing HTTP clients during "faults" like "connection closed in middle of body". | [![Build Status](https://dev.azure.com/azure-sdk/internal/_apis/build/status/tools/tools%20-%20http-fault-injector?branchName=main)](https://dev.azure.com/azure-sdk/internal/_build/latest?definitionId=2340&branchName=main)                                                |
| Maven Plugin for Snippets      | [Readme](packages/java-packages/snippet-replacer-maven-plugin/README.md)               | A Maven plugin that that updates code snippets referenced from javadoc comments.                                             | Not Yet Enabled                                                                                                                                                                                                                            |
| pixel insertion tool           | [Readme](scripts/python/readme_tracking/readme.md)      | A tool used to insert the requests for images served by `pixel server`.              | Not Yet Enabled                                                                                                                                                                                                                            |
| pixel-server                   | [Readme](web/pixel-server/README.md)                   | A tiny ASP.NET Core site used to serve a pixel and record impressions.               | Not Yet Enabled                                                                                                                                                                                                                            |

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

[![Azure DevOps builds](https://img.shields.io/azure-devops/build/azure-sdk/internal/1372?label=eng%2Fcommon%20sync)](https://dev.azure.com/azure-sdk/internal/_build/latest?definitionId=1372&branchName=main)

C:/repo/sdk-tools/packages/java-packages/snippet-replacer-maven-plugin
