# Azure Data SDK Content Validation Automation

## Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Visual Studio](https://visualstudio.microsoft.com/downloads/)

## Quickstart

There are 6 projects in this solution.

- **ContentValidation**: Libraries container all verify rules.
- **ContentValidation.Test**: Contains all test cases for content validation automation.
- **DataSource**: Provide test data for test cases (all pages need to be verified on [Microsoft Learn website](https://learn.microsoft.com/en-us/python/api/overview/azure/?view=azure-python), all documents in this repository will use Python as examples. We plan to expand to Java/JS/.NET/Go in the future.)
- **IssuerHelper**: Summarize the issue status of the current pipeline run and record it in the latest-pipeline-result branch.
- **ReportHelper**: Compare the total issue data of current pipeline with the summary data of the last pipeline, save it as excel or json file, and provide text in markdown format (used when creating/update github issues).

- **ValidationRule.Test**: Contains all test cases for validation rules. (This test is a static data test, which is designed to ensure that there are no logical bugs when the rules are modified.)


This quickstart will show you how to use this tool to fetch all test data and run test cases locally.

1. Clone this repo and open it with Visual Studio.
2. Update the `DataSource/appsettings.json` file and replace its content with the package you need to test. When testing locally, you can only run one package at a time. Please note that the `Branch`, `CookieName` and `CookieValue` parameters are used for internal version testing, so you do not need to change them. If you are not sure how to fill in the `ReadmeName` parameter, you can find the `yml` file of the package you want to test in the `/eng/pipelines/language/package` folder, where you can find the corresponding parameters. For example:
```json
{
  "ReadmeName": "appconfiguration-readme",
  "Language": "Python",
  "Branch": "main",
  "CookieName": "",
  "CookieValue": ""
}
```
3. Run `DataSource` with Visual Studio. After that, you will see an `appsettings.json` file generated under `ContentValidation.Test` folder. 
4. Switch to `ContentValidation.Test` project and execute test cases.

The above steps can help you quickly run content validation tests for a single package locally. 

However, the goal of this tool is to automatically (regularly) run tests after deployment, automatically create issues on GitHub for failed tests, and finally generate a test report in markdown form. For the content of this part, please refer to the [deployment.md](/docs/deployment.md) file.

## Index

| Project or Library | Path | Description | 
| ------- | ---- | ----------- |
| ContentValidation | [md](/ContentValidation/README.md#overview) | Libraries container all verify rules. |
| ContentValidation.Test | [md](/ContentValidation.Test/README.md#overview) | Contains all test cases for content validation automation.|
| DataSource | [md](/DataSource/README.md#overview) | Provide test data for test cases. |
| IssuerHelper | [md](/IssuerHelper/README.md#overview) | Handling issue related. |
| ReportHelper | [md](/ReportHelper/README.md#overview) | Generate total/diff issues. |
| ValidationRule.Test | [md](/ValidationRule.Test/README.md#overview) | Contains all test cases for validation rules. |

## Contributing
This project welcomes contributions and suggestions. Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit https://cla.microsoft.com.