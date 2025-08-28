# Data Source Project

## Overview

The Data Source Project is used to obtain the test data source (all pages need to be verified on [Microsoft Learn website](https://learn.microsoft.com/en-us/python/api/overview/azure/?view=azure-python)).

## Getting started

Need to configure `ReadmeName` and `Language` in the `appsettings.json` file in advance. The remaining parameters are for internal testing. To use this function in Azure DevOps Pipeline, you need to have access to internal pages and configure `CookieName` and `CookieValue` in advance. `Branch` only needs to be entered when running Pipeline.

```json
{
  "ReadmeName": "appconfiguration-readme",
  "Language": "Python",
  "Branch": "main",
  "PackageName": "azure-appconfiguration",
  "CsvPackageName": "",
  "CookieName": "",
  "CookieValue": ""
}
```

>Notes: Taking Python as an example, ReadmeName parameter need to be filled in strictly according to the format given in the Python SDK doc, otherwise the code will not be able to obtain all the links to be tested for the relevant package.

After configuration, you can run this project directly. After running, you will find that test data has been generated in the [ContentValidation.Test/appsettings.json](../ContentValidation.Test/appsettings.json) file.

## Config.json

Since Playwright requires an initial link to fetch the data source, and then parses all the test links layer by layer based on it, we need to construct this link at the very beginning. Such as: `https://learn.microsoft.com/en-us/python/api/overview/azure/monitor-ingestion-readme?view=azure-python`. In this link, we only need to handle `monitor-ingestion-readme`, as the rest is fixed. Most of the initial links follow a common pattern:

```dotnetcli
                var langKey = language.ToLower();
                switch (langKey)
                {
                    case "python":
                        readme = package.Replace("azure-", "") + "-readme";
                        break;
                    case "java":
                        readme = package.Replace("azure-", "") + "-readme";
                        break;
                    case "javascript":
                        readme = package.Replace("azure-", "") + "-readme";
                        package = package.Replace("azure-", "@azure/");
                        break;
                    case "dotnet":
                        readme = package.Replace("azure-", "").Replace("-", ".") + "-readme";
                        package = ToPascalWithDots(package);
                        break;
                    default:
                        throw new ArgumentException($"Unsupported language specified: {langKey}");
                }
```

Some packages do not follow the general rule. If handled this way, the constructed initial link will result in errors such as *page not found* or *timeout* when Playwright tries to access it. Therefore, you need to add these packages to the `config.json` file and provide the correct configuration for them.

```json
  "python": {
    "azure-functions-durable": {
      "readme": "functions/durablefunctions"
    },
    "azure-functions": {
      "readme": "functions/functions"
    },
    "azure-iot-device": {
      "readme": "iot/iotdevice"
    },

    "azure-iot-hub": {
      "readme": "iot/iothub"
    }
  }
```