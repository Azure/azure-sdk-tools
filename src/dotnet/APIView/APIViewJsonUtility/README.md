# APIViewJsonUtility

### Overview

APIViewJsonUtility is a .NET utility designed to facilitate the manipulation and processing of JSON data within the APIView ecosystem. This tool helps developers to efficiently handle JSON data structures, making it easier to integrate with APIView's functionalities when working on parser to verify how JSON token file is rendered as output text on APIView without uploading JSON file to APIView.

### Features

- **Create API view output from JSON token**: Parse JSON token file to create the API review output text.
- **Convert to tree token model**: Convert an old flat list tree token JSON file to new tree style JSON token file.


### Prerequisites

- .NET SDK (version 8.0 or later)

### Installation

APIViewJsonUtility is published to Azure DevOps artifact. You can install this .NET tool from [azure-sdk-for-net feed](https://dev.azure.com/azure-sdk/public/_artifacts/feed/azure-sdk-for-net/NuGet/APIViewJsonTool/overview) using below command.

`dotnet tool install --add-source "https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-net/nuget/v3/index.json" --version 1.0.0-dev.20240919.3 --tool-path <installation path> APIViewJsonTool
`

### Usage

Here are two examplea of how to use APIViewJsonUtility:

- Create APIView output text

    `.\APIViewJsonUtility.exe --path  <Path to JSON tree token file> --dumpApiText`

- Convert Token file to tree model from flat list JSON

    `.\APIViewJsonUtility.exe --path  <Path to flat list JSON token file> --convertToTree`