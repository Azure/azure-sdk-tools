# `Azure SDK CLI`

The Azure SDK Engineering System's automation server that is intended to encapsulate manual work in the `azure sdk` release process.

## Prerequisites

- .NET 8.0 (`winget install Microsoft.DotNet.SDK.8`)
- Visual Studio Code (`winget install Microsoft.VisualStudioCode`)
  - [Copilot Extension](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot)
  - [C# Dev Kit Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) (optional)

## Quick Start

1. **Open VS Code** in the `azure-sdk-tools` directory
2. **Start the MCP server** (optional - Copilot will auto-start if needed):
   - In `.vscode/mcp.json`, click the Start button below "servers"
  
   ![Screenshot showing the MCP Start button in VS Code's mcp.json file](/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Images/MCP-Start.png)

3. **Test the connection** by prompting Copilot (`Ctrl + Shift + I`):

   ```text
   "Use the hello-world MCP tool to echo back 'Testing the tool'"
   ```

    ![Screenshot showing Github Copilot successfully interacting with the MCP server.](/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Images/MCP-Success-Output.png)

## Usage Modes

```text
Description:
  azsdk cli - A Model Context Protocol (MCP) server that enables various tasks for the Azure SDK Engineering 
  System.

Usage:
  azsdk [command] [options]

Options:
  --tools <tools>        If provided, the tools server will only respond to CLI or MCP server requests for      
                         tools named the same as provided in this option. Glob matching is honored.
  --debug                Enable debug logging [default: False]
  -o, --output <output>  The format of the output. Supported formats are: plain, json [default: plain]
  --version              Show version information
  -?, -h, --help         Show help and usage information

Commands:
  azp                  Azure Pipelines Tool
  eng                  Internal azsdk engineering system commands
  log                  Log processing commands
  start                Starts the MCP server (stdio mode)
  release-plan         Manage release plans in AzureDevops
  spec-tool            TypeSpec project tools for Azure REST API Specs
  spec-pr              Pull request tools
  spec-workflow        Tools to help with the TypeSpec SDK generation.
  validate-typespec    Run typespec validation
  releaseReadiness     Checks release readiness of a SDK package.
  hello-world <input>  Tests echoing a message back to the client
```

### 1. MCP Server Mode

Add to `.vscode/mcp.json` file, ensuring that the type is set to `stdio`:

**Using standalone executable:**

```jsonc
// within mcp.json "servers" section:
"Azure SDK Everything": {
  "type": "stdio",
  "command": "azsdk.exe",
  "args": ["start"]
}
```

**Using dotnet tool:**

```jsonc
"Azure SDK Everything": {
  "type": "stdio", 
  "command": "dotnet",
  "args": ["run", "--", "start"],
  "cwd": "path/to/AzureSdk.Tools.Cli"
}
```

### 2. Standalone CLI Mode

Run directly as a command-line tool:

- ```bash
  $WORKITEMID=12345
  dotnet run -- release-plan get --work-item-id $WORKITEMID
  ```

In either case, the _same_ code will be invoked to get both results.

This server is intended to run **locally only** and will utilize your environment cached settings to communicate where authentication is necessary.
