# `Azure SDK CLI`

This implementation is the Azure SDK Engineering System's `everything` server.

It is eventually intended to encapsulate a lot of the manual work in an `azure sdk` package's release process.

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
