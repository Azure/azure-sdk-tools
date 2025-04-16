# AzureSDKDevToolsMCP

AzureSDKDevToolsMCP is a .NET 9.0 web application designed to integrate with the Model Context Protocol (MCP) framework. It provides tools for service team to use in GitHub Copilot agent to run various TypeSpec based operations.
For e.g. TypeSpec validation

## Features

- **TypeSpec Validation**: Includes tools to validate TypeSpec API specifications.
- **MCP Integration**: Leverages the Model Context Protocol for server communication.
- **Environment Configurations**: Supports both development and production configurations.

## Prerequisites

- .NET 9.0 SDK
- Node.js and npm (for TypeSpec validation)

## Getting Started
This tool will be published as a static executable but currently it is available to run from source only.

1. Clone the repository:
   ```bash
   git clone https://github.com/azure/azure-sdk-tools.git
   ```

2. Navigate to the project directory:
   ```bash
   cd tools/mcp/AzureSDKDevTools
   ```

3. Restore dependencies:
   ```bash
   dotnet restore
   ```

4. Run the application:
   ```bash
   dotnet run
   ```

5. Access the application at:
   - HTTP: `http://localhost:5134/sse`
   - HTTPS: `https://localhost:7133/sse`

6. Add an entry in VS Code MCP server settings for `AzureSDKDevTools` by copying below JSON into MCP settings.

Use CTRL+SHIFT+P and select `Open User Settings(JSON)` and add below MCP settings to enable VS code to connect to MCP server.
```
"mcp": {
        "servers": {
            "AzureSDKDevTools": {
                "type": "sse",
                "url": "http://localhost:5134/sse"
            }
        }
    }
```

## Tools

### SpecValidationTool

The `SpecValidationTool` validates TypeSpec specifications for Azure SDK services. It ensures compliance with TypeSpec standards.

#### Usage

1. Open GitHub Copilot window and select `Agent` mode.
2. Prompt copilot to run TypeSpec validation for a TypeSpec project. For e.g. `Run TypeSpec validation for Contoso.WidgeManager` or `Validate TypeSpec for for Contoso.WidgeManager`


## License

This project is licensed under the MIT License. See the LICENSE file for details.