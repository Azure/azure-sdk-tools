# AzureSDKDevToolsMCP

AzureSDKDevToolsMCP is a .NET 8.0 web application designed to integrate with the Model Context Protocol (MCP) framework. It provides tools for service team to use in GitHub Copilot agent to run various TypeSpec based operations.
For e.g. TypeSpec validation

## Features

- **TypeSpec Validation**: Includes tools to validate TypeSpec API specifications.
- **MCP Integration**: Leverages the Model Context Protocol for server communication.
- **Environment Configurations**: Supports both development and production configurations.

## Prerequisites

- .NET 8.0 SDK
- Node.js and npm (for TypeSpec validation)
- GitHub client (for authentication)

## Getting Started
Compile and build the tools to generate static exe.

1. Clone the repository:
   ```
   git clone https://github.com/azure/azure-sdk-tools.git
   ```

2. Navigate to the project directory:
   ```
   cd tools/mcp/AzureSDKDevTools
   ```

3. Build the project:
   ```
   dotnet build .
   ```

4. Generate the static executable:
   ```
   dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:GenerateBuildInfoConfigFile=false
   ```

5. Add an entry in VS Code MCP server settings for `AzureSDKDevTools` by copying below JSON into mcp.json.

```
{  
    "servers": {
      "Azure SDK Dev Tools": {
        "type": "stdio",
        "command": "<Path to exe>\\AzureSDKDSpecTools.exe",
    }
  }
}
```

## Tools

### SpecValidationTool

The `SpecValidationTool` validates TypeSpec specifications for Azure SDK services. It ensures compliance with TypeSpec standards.

#### Usage

1. Open GitHub Copilot window and select `Agent` mode.
2. Prompt copilot to run TypeSpec validation for a TypeSpec project. For e.g. `Run TypeSpec validation for Contoso.WidgeManager` or `Validate TypeSpec for Contoso.WidgeManager`


## License

This project is licensed under the MIT License. See the LICENSE file for details.