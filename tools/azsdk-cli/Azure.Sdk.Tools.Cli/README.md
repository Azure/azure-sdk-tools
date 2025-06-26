# `Azure SDK CLI`

This implementation is the Azure SDK Engineering System's `everything` server.

It is eventually intended to encapsulate a lot of the manual work in an `azure sdk` package's release process.

## Get Started

1. Open VS Code in the `azure-sdk-tools` directory.
2. Within the mcp.json file press the Start button below "servers"
![Local Image](/tools/azsdk-cli/Azure.Sdk.Tools.Cli/image/MCP-Start.png)
3. Prompt Copilot in agent mode to `"Use the hello-world MCP tool to echo back 'Testing the tool'".`

This server is intended to be run in one of two ways:

- As a standalone `MCP` server that can be added directly to a `.vscode/mcp.json` file through `stdio`.
  - Downloaded standalone exe:
    - ```jsonc
      // within mcp.json `servers` member:
      "Azure SDK Everything": {
        "type": "stdio",
        "command": "path/to/Azure.Sdk.Tools.Cli.exe",
        "args": [
          "start"
        ]
      }
      ```
  - Directly as a `dotnet tool install`-ed dotnet tool
    - ```jsonc
      "Azure SDK Everything": {
        "type": "stdio",
        "command": "azsdk-cli",
        "args": [
          "start"
        ]
      }
      ```
- As a standalone tool encapsulating specific tool usage
  - ```bash
    WORKITEMID=12345
    azsdk-cli get-release-plan $WORKITEMID
    ```

In either case, the _same_ code will be invoked to get both results.

This server is intended to run **locally only** and will utilize your environment cached settings to communicate where authentication is necessary.
