# The engsys `everything` server

This tool will serve as the primary integration point for all of the `azure-sdk` provided MCP tools. This tool will be built and published out of the `azure-sdk-tools` repo, but consumed primarily through the `.vscode/mcp.json` within each `Azure-sdk-for-X` language repo. Tool installation will be carried out by the eng/common scripts present at `eng/common/mcp/azure-sdk-mcp.ps`.

## Some random DOs and DON'Ts of this server

- [x] DO build with the idea that authentication WILL be coming. Right now the sole protection we have is that these tools will be running in context of the "current user." This means access to `DefaultAzureCredential` should be enough to allow it to function. Users will be adding other external servers to their `mcp.json` at their own risk.
  - What does this actually look like in practice?
- [x] Provide `--tools` startup parameter:
  - Provide `--tools <name>,<name>,<name>` to _enable_ specific functionalties of the `hub` server?
  - Provide `--tools-exclude <name>,<name>,<name>` to _disable_ specific functionalities of the `hub` server?
-