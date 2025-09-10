# Azure.Sdk.Tools.Cli.Contract

This package contains the definition of the abstract classes `MCPToolBase`, `MCPTool` and `MCPMultiCommandTool`.

To add additional functionality to `Azure.Sdk.Tools.Cli`, a user need only:
 - Add a reference on `Azure.Sdk.Tools.Cli.Contract` within the project generating their DLL
 - Add a class inheriting from `MCPTool` or `MCPMultiCommandTool` in their package. Implement necessary functions.
 - Submit a PR to `Azure.Sdk.Tools.Cli` adding a reference to _their_ package.
 - Assembly discovery in `Azure.Sdk.Tools.Cli` should automatically pull in their new tool