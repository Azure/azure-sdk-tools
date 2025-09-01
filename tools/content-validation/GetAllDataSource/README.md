# GetAllDataSource Project

## Overview

The `GetAllDataSource` project is designed to retrieve test data sources for validating SDK documentation pages. It supports fetching data for all pages that need verification on the [Microsoft Learn website](https://learn.microsoft.com/en-us/).  

> **Note:** Currently, this project supports SDK documentation for Python, Java, JavaScript, and .NET.

## Features

- Fetches SDK documentation links for multiple programming languages.
- Supports Azure DevOps Pipeline integration.
- Automatically determines the version suffix (GA or Preview) for SDK packages.
- Generates test data for validation purposes.

## Configuration

To use this project, you need to configure the `appsettings.json` file with the required parameters. Below is an example configuration:

```json
{
  "Branch": "main",
  "CookieName": "",
  "CookieValue": "",
  "Languages": {
    "DotNet": [
      {
        "ReadmeName": "ai.agents.persistent-readme",
        "PackageName": "azure-ai-agents-persistent",
        "CsvPackageName": "Azure.Ai.Agents.Persistent"
      }
    ],
    "Java": [
      {
        "ReadmeName": "ai-contentsafety-readme",
        "PackageName": "azure-ai-contentsafety",
        "CsvPackageName": "azure-ai-contentsafety"
      }
    ],
    "JavaScript": [
      {
        "ReadmeName": "ai-language-text-readme",
        "PackageName": "azure-ai-language-text",
        "CsvPackageName": "@azure/ai-language-text"
      }
    ],
    "Python": [
      {
        "ReadmeName": "ai-contentsafety-readme",
        "PackageName": "azure-ai-contentsafety",
        "CsvPackageName": "azure-ai-contentsafety"
      }
    ]
  }
}
```

### Key Parameters

- **Branch**: The branch name, required for pipeline execution.
- **CookieName** and **CookieValue**: Required for accessing internal pages.
- **Languages**: Contains configurations for SDK packages across different programming languages.

## Usage

1. Configure the `appsettings.json` file as described above.
2. Run the project directly using your preferred method (e.g., command line or IDE).
3. Upon execution, the test data will be generated and saved for validation purposes.

## Notes

- Ensure strict adherence to the `ReadmeName` format for SDK documentation.
- Internal testing parameters are optional and can be left blank if not required.

This project is essential for ensuring the accuracy and completeness of SDK documentation validation processes.