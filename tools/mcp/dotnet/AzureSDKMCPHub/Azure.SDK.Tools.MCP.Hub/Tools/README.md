# Writing tools that integrate with the MCP server

## How to create your tool code

- Inherit from `Azure.SDK.Tools.MCP.Contract::MCPHubTool`
- Implement the necessary functions

## To quickly dev loop on just your class.

- Ensure that your class in `Tools` is inheriting from the abstract class `MCPHubTool.`
- Update the `debug` properties for the `hub` project to target a specific tool `--tool <nameofyourclass>`

## Before publishing a PR

- Invoke the tests in `Azure.SDK.Tools.MCP.Hub.Tests` which will exercise "load everything" to prevent accidental breaks based on duplicate tool names etc.
