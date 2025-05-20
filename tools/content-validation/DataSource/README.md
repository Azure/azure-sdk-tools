# Data Source Project

## Overview
The Data Source Project is used to obtain the test data source (all pages need to be verified on [Microsoft Learn website](https://learn.microsoft.com/en-us/python/api/overview/azure/?view=azure-python)).
>Notes: currently only for getting Python and Java SDK doc.

## Getting started
Need to configure `ReadmeName` and `Language` in the `appsettings.json` file in advance. The remaining parameters are for internal testing. To use this function in Azure DevOps Pipeline, you need to have access to internal pages and configure `CookieName` and `CookieValue` in advance. `Branch` only needs to be entered when running Pipeline.
```json
{
  "ReadmeName": "appconfiguration-readme",
  "Language": "Python",
  "Branch": "main",
  "CookieName": "",
  "CookieValue": ""
}
```
>Notes: Taking Python as an example, ReadmeName parameter need to be filled in strictly according to the format given in the Python SDK doc, otherwise the code will not be able to obtain all the links to be tested for the relevant package.

After configuration, you can run this project directly. After running, you will find that test data has been generated in the [ContentValidation.Test/appsettings.json](../ContentValidation.Test/appsettings.json) file.