# `Azure SDK CLI`

The Azure SDK Engineering System's automation server that is intended to encapsulate manual work in the `azure sdk` package's release process.

## Prerequisites

- .NET 8.0 (`winget install Microsoft.DotNet.SDK.8`)
- Visual Studio Code (`winget install Microsoft.VisualStudioCode`)
  - [Copilot Extension](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot)
  - [C# Dev Kit Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) (optional)

## Quick Start

1. **Open VS Code** in the `azure-sdk-tools` directory
2. **Start the MCP server** (optional - Copilot will auto-start if needed):
   - In `.vscode/mcp.json`, click the Start button below "servers"
   
   ![Local Image](/tools/azsdk-cli/Azure.Sdk.Tools.Cli/image/MCP-Start.png)

3. **Test the connection** by prompting Copilot (`Ctrl + Shift + I`):

   ```text
   "Use the hello-world MCP tool to echo back 'Testing the tool'"
   ```

## Usage Modes

### 1. MCP Server Mode

Add to `.vscode/mcp.json` file through `stdio`:

**Using standalone executable:**

```jsonc
// within mcp.json "servers" section:
"Azure SDK Everything": {
  "type": "stdio",
  "command": "path/to/Azure.Sdk.Tools.Cli.exe",
  "args": ["start"]
}
```

**Using dotnet tool:**

```jsonc
"Azure SDK Everything": {
  "type": "stdio", 
  "command": "azsdk-cli",
  "args": ["start"]
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
