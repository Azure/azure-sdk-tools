# Writing tools that integrate with the MCP server

## How to create your tool code

- Inherit from `Azure.Sdk.Tools.Cli.Contract::MCPHubTool`
- Ensure your class has the `[McpServerToolType]` attribute added to it
  - Eventual goal is to automatically recognize any classes that have this type as `[McpServerToolType]`.
  - For now you need to add the attribute yourself.
- Mark each function with that should be visible to MCP with `[McpServerTool]` attribute.
- Create an override `GetCommand` and `HandleCommand`
- Place your code under a _folder_ named the same as your Tool class name.
  - See `ReleasePlanTool` for an example of this folder structure.

## Restrictions on `McpServerTool`-attributed methods

There does not yet exist a methodology to ensure unhandled exceptions are caught and returned in a valid format to the MCP caller. As such, we need to ensure that we are not throwing unhandled exceptions in ANY case.

To deal with this, an analyzer exists for this project that will ensure that any method marked with the `McpServerTool` attribute is not throwing unhandled exceptions.

To get around this warning, a user only needs to ensure that the **entire body** of the method is wrapped in a try/catch block. The catch block should return a response that is appropriate for the method being called.

See examples in `SpecPullRequestTools.GetGitHubUserDetails` and `SpecPullRequestTools.GenerateSDK`.

## To quickly dev loop on just your class

- Ensure that your class in `Tools` is inheriting from the abstract class `MCPHubTool.`
- Update the `debug` properties for the `hub` project to target a specific tool `--tool <nameofyourclass>`

## Before publishing a PR

- Invoke the tests in `Azure.Sdk.Tools.Cli.Tests` which will exercise "load everything" to prevent accidental breaks based on duplicate tool names etc.
