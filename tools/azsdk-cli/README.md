# Azure SDK CLI and MCP server

This project is the primary integration point for all `azure-sdk` provided [MCP](https://modelcontextprotocol.io/introduction) tools as well as a convenient CLI app for azure sdk developers. It is built and published out of the `azure-sdk-tools` repo but consumed primarily through the `.vscode/mcp.json` file within each `Azure-sdk-for-<lang>` language repository. Server installation is carried out by the eng/common scripts present at `eng/common/mcp/azure-sdk-mcp.ps1`.

* [Getting Started](#getting-started)
  * [Prerequisites](#prerequisites)
  * [Setup](#setup)
  * [Build](#build)
  * [Run](#run)
  * [Test](#test)
* [Project Structure and Information](#project-structure-and-information)
  * [Directory Structure](#directory-structure)
  * [Pipelines](#pipelines)
* [Adding a New Tool](#adding-a-new-tool)

## Getting Started

### Prerequisites

- [.NET 8.0 SDK or later](https://dotnet.microsoft.com/download)

### Setup

1. Clone the repository:
    ```sh
    git clone https://github.com/Azure/azure-sdk-tools.git
    cd azure-sdk-tools/tools/azsdk-cli
    ```

2. Restore dependencies:
    ```sh
    dotnet restore
    ```

### Build

To build the project:

```sh
dotnet build
```

### Run

To run the CLI locally:

```sh
dotnet run --project Azure.Sdk.Tools.Cli -- --help
```

### Test

To run the tests:

```sh
dotnet test
```

## Project Structure and Information

This project is both a [System.CommandLine](https://learn.microsoft.com/en-us/dotnet/standard/commandline/) app and an MCP server using the [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

### Directory Structure

- *Azure.Sdk.Tools.Cli* - Core project for cli/mcp logic
    - *Commands* - Shared classes for CLI commands
    - *Configuration* - Constants and other classes
    - *Helpers* - Helper logic for parsing data
    - *Models* - Shared models, response classes and schemas
    - *Services* - Helper classes for working with upstream services, e.g. azure, devops, github
    - *Tools* - CLI commands and MCP tool implementations
- *Azure.Sdk.Tools.Cli.Tests* - Test project
- *Azure.Sdk.Tools.Cli.Contract* - Common classes/interfaces
- *Azure.Sdk.Tools.Cli.Analyzer* - Compilation addons to enforce conventions
    - Enforce all tools handled by try/catch
    - Enforce tool inclusion in service registration for dependency injector

### Pipelines

Public CI - https://dev.azure.com/azure-sdk/public/_build?definitionId=7677

Release - https://dev.azure.com/azure-sdk/internal/_build?definitionId=7684 

## Design Guidelines

- Think of the server primarily as a first class CLI app
    - Add attributes to enable MCP hooks, but MCP server functionality is a pluggable feature, not foundational to the architecture
    - Rapid ad-hoc testing is easier via CLI than MCP, and any tools we build can be consumed by other software/scripts outside of MCP
    - For example, the engsys/azsdk cli app is built around System.CommandLine along with some dependency injection and ASP.net glue + attributes to get it working with the MCP C# sdk
- Return structured data from all tools/commands. Define response classes that can `ToString()` or `ToJson()` for different output modes (and handle failure flows)
- Write debug logging to stderr and/or a file in MCP mode. This avoids the misleading "FAILURE TO PARSE MESSAGE" type errors in the MCP client logs
- Support both stdio and http mode for MCP to enable easy debugging with tools like mcp inspector
- Where possible, avoid dependencies/pre-requisites requiring manual setup, prefer being able to set them up within the app (e.g. az login, gh login, etc.)

## Adding a New Tool

Tool classes are the core of the azure sdk app and implement CLI commands or MCP server tools. To add a new tool start with the following:

- Determine the right directory and tool class name under `Azure.Sdk.Tools.Cli/Tools`
- Reference or copy from the [hello world tool](Azure.Sdk.Tools.Cli/Tools/HelloWorldTool/HelloWorldTool.cs) for an example.
- See [Tools/README.md](./Azure.Sdk.Tools.Cli/Tools/README.md) for more details

Each tool must implement a `GetCommand()` method and a `HandleCommand(...)` method. These allow the tool to be called from CLI mode.

```csharp
public override Command GetCommand()
{
    Command command = new("example", "An example CLI command");
    command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
    return command;
}

public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
{
    var result = await SomeMethod();
    ctx.ExitCode = ExitCode;
    output.Output(result);
}
```

Additionally, the tool class and any methods that will be included in the MCP server tools list must have the MCP attributes:

```csharp
[McpServerToolType, Description("Example tool")]
public class ExampleTool(ILogger<ExampleTool> logger, IOutputService output) : MCPTool
{
    [McpServerTool(Name = "example-1"), Description("Example tool call 1")]
    public DefaultCommandResponse Success(string message)
    {
        try
        {
            return new()
            {
                Message = "success"
            }
        }
        catch (Exception ex)
        {
            SetFailure(1);
            return new()
            {
                ResponseError = $"failure: {ex.Message}"
            }
        }
    }
}
```

**Rather than bubbling up exceptions, all `McpServerTool` methods must handle failures and format them into its response object. This allows tools to be interoperable between CLI and MCP mode.**
