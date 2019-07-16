# Azure SDK Tools

This repository contains useful tools that the Azure SDK team utilizes across their infrastructure.

## Index

| Package or Intent              | Path                                                    | Description                                                                          | Status                                                                                                                                                                                                                                     |
| ------------------------------ | ------------------------------------------------------- | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| doc-warden                     | [Readme](packages/python-packages/doc-warden/README.md) | A tool used to enforce readme standards across Azure SDK Repos.                      | [![Build Status](https://dev.azure.com/azure-sdk/public/_apis/build/status/108?branchName=master)](https://dev.azure.com/azure-sdk/public/_build/latest?definitionId=108&branchName=master)                                                |
| pixel-server                   | [Readme](/web/pixel-server/README.md)                   | A tiny ASP.NET Core site used to serve a pixel and record impressions.               | Not Yet Enabled                                                                                                                                                                                                                            |
| pixel insertion tool           | [Readme](scripts/python/readme_tracking/readme.md)      | A tool used to insert the requests for images served by `pixel server`.              | Not Yet Enabled                                                                                                                                                                                                                            |
| @azure/eslint-plugin-azure-sdk | [Readme](/tools/eslint-plugin-azure-sdk/README.md)      | An ESLint plugin enforcing design guidelines for the JavaScript/TypeScript Azure SDK | [![Build Status](https://dev.azure.com/azure-sdk/public/_apis/build/status/tools/tools%20-%20eslint-plugin-azure-sdk%20-%20ci?branchName=master)](https://dev.azure.com/azure-sdk/public/_build/latest?definitionId=628&branchName=master) |

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
