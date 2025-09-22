# New Tool Development Guide

This guide provides comprehensive instructions for creating new Tool classes in the `azsdk-cli` project. These tools serve dual purposes: they can be invoked via the command-line interface (CLI) and exposed through the Model Context Protocol (MCP) server for LLM coding agents.

New tools can be created with copilot chat/agent:

```
Help me create a new tool using #new-tool.md as a reference
```

## Table of Contents

   * [Tool Architecture Overview](#tool-architecture-overview)
   * [Step-by-Step Implementation Guide](#step-by-step-implementation-guide)
   * [Code Examples and Templates](#code-examples-and-templates)
   * [Dependency Injection](#dependency-injection)
   * [Response Handling](#response-handling)
   * [Registration and Testing](#registration-and-testing)
   * [Required Tool Conventions](#required-tool-conventions)
   * [Common Patterns and Anti-patterns](#common-patterns-and-anti-patterns)

## Tool Architecture Overview

All tools in the azsdk-cli project follow a consistent architecture:

- **Location**: Tool files are organized under [`Azure.Sdk.Tools.Cli/Tools/`](../Azure.Sdk.Tools.Cli/Tools/{Category}/) in logical categories
- **Namespace**: Tools should be in namespace `Azure.Sdk.Tools.Cli.Tools.{Category}`
- **Base Class**: All tools inherit from [`MCPTool`](../Azure.Sdk.Tools.Cli.Contract/MCPTool.cs) or [`MCPMultiCommandTool`](../Azure.Sdk.Tools.Cli.Contract/MCPMultiCommandTool.cs)
- **Attributes**: Tools are decorated with `[McpServerToolType]` for discovery
- **Dual Interface**: Tools support both CLI commands and MCP server methods

### Tool Structure Components

1. **Class Declaration**: Inherits from `MCPTool` or `MCPMultiCommandTool` with appropriate attributes
2. **Constructor**: Uses dependency injection to receive required services
3. **Command Configuration**: CLI options, arguments, and command hierarchy
4. **CLI Handler**: `GetCommand()` (for `MCPTool`) or `GetCommands()` (for `MCPMultiCommandTool`) and `HandleCommand()` methods
5. **MCP Methods**: Methods decorated with `[McpServerTool]` for LLM access
6. **Error Handling**: Comprehensive try/catch blocks and response error management

## Step-by-Step Implementation Guide

### Step 1: Determine Tool Placement and Naming

**Questions to Consider:**
- What is the primary function of your tool?
- Does it fit into an existing command group or need a new one?
- Does it fit into an existing namespace based on the primary function?
- What should the CLI command structure look like?

**Naming Conventions:**
- **Class Name**: `{FunctionalName}Tool` (e.g., `LogAnalysisTool`, `PipelineAnalysisTool`)
- **File Location**: [`Tools/{Category}/{ToolName}.cs`](../Azure.Sdk.Tools.Cli/Tools/)
- **Namespace**: `Azure.Sdk.Tools.Cli.Tools.{Category}` (namespace category should be choosen based on the primary function)

### Step 2: Define Command Group and Structure

**Command Groups** (defined in [`SharedCommandGroups.cs`](../Azure.Sdk.Tools.Cli/Commands/SharedCommandGroups.cs)):
- `AzurePipelines` - Azure DevOps pipeline operations (`azsdk azp`)
- `EngSys` - Engineering system commands (`azsdk eng`)
- `Generators` - File generation commands (`azsdk generators`)
- `Cleanup` - Resource cleanup commands (`azsdk cleanup`)
- `Log` - Log processing commands (`azsdk log`)
- `Package` - Package commands (`azsdk package`)
- `Tsp` - Typespec commands (`azsdk tsp`)

**Command Hierarchy Examples:**
```csharp
// Single group: azsdk log <sub-command>
public override CommandGroup[] CommandHierarchy { get; set; } = [ SharedCommandGroups.Log ];

// Multiple groups: azsdk eng cleanup <sub-command>
public override CommandGroup[] CommandHierarchy { get; set; } = [
    SharedCommandGroups.Example,
    SharedCommandGroups.Demo
];
```

### Step 3: Plan CLI Arguments and Options

**Decision Points:**
- **Arguments**: Required positional parameters (e.g., file paths, IDs)
- **Options**: Optional flags and parameters with default values
- **Sub-commands**: Does your tool need multiple operations?

**Shared Options** (refer to [`SharedOptions.cs`](../Azure.Sdk.Tools.Cli/Commands/SharedOptions.cs)) for options used broadly across commands

### Step 4: Design MCP Methods

**Consider:**
- What methods should be exposed to LLM agents?
- What parameters do they need?
- How should responses be structured?
- What error conditions need handling?

### Step 5: Identify Dependencies

**Common Dependencies:**
- `ILogger<YourTool>` - Always required for logging
- `IProcessHelper` - Helper for running external processes
- `IAzureService` - For Azure authentication and credentials
- `IDevOpsService` - For Azure DevOps operations
- Custom service interfaces for your tool's specific needs

## Code Examples and Templates

A working example of multiple tool types and usage of services can be found at [`ExampleTool.cs`](../Azure.Sdk.Tools.Cli/Tools/Example/ExampleTool.cs)

Additional documents exist that detail more specific scenarios:

- [Process Calling](./process-calling.md)

### Basic Tool Template

In [`Azure.Sdk.Cli.Tools.Cli/Tools/YourToolCategory/YourTool.cs`](../Azure.Sdk.Tools.Cli/Tools/):

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.YourToolCategory;

[McpServerToolType, Description("Brief description of what this tool does")]
public class YourTool(
    ILogger<YourTool> logger
) : MCPTool
{
    // Set command hierarchy - determines CLI command path
    public override CommandGroup[] CommandHierarchy { get; set; } = [
        SharedCommandGroups.YourGroup,
    ];

    // CLI Options and Arguments
    private readonly Argument<string> requiredArg = new Argument<string>(
        name: "input",
        description: "Description of required argument"
    ) { Arity = ArgumentArity.ExactlyOne };

    private readonly Option<string> optionalParam = new(["--param", "-p"], "Optional parameter description");
    private readonly Option<bool> flagOption = new(["--flag", "-f"], () => false, "Boolean flag description");

    // CLI Command Configuration
    protected override Command GetCommand() =>
        new("your-command", "Description for CLI help")
        {
            requiredArg, optionalParam, flagOption
        };

    // CLI Command Handler
    public override async Task<CommandResponse> HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        // Extract parameters from CLI context
        var input = ctx.ParseResult.GetValueForArgument(requiredArg);
        var param = ctx.ParseResult.GetValueForOption(optionalParam);
        var flag = ctx.ParseResult.GetValueForOption(flagOption);

        // Call your main logic (can be shared with MCP methods)
        return await ProcessRequest(input, param, flag, ct);
    }

    // MCP Server Method - exposed to LLM agents
    [McpServerTool(Name = "azsdk_your_tool_method"), Description("Description for LLM agents")]
    public async Task<YourResponseType> ProcessRequest(string input, string? optionalParam = null, CancellationToken ct = default)
    {
        try
        {
            return await ProcessRequest(input, optionalParam, false, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing MCP request: {input}", input);
            return new YourResponseType
            {
                ResponseError = $"Error processing request: {ex.Message}"
            };
        }
    }
}
```

### Complex Tool Example (Multiple Sub-commands)

```csharp
[McpServerToolType, Description("Tool with multiple sub-commands")]
public class ComplexTool(
    ILogger<ComplexTool> logger,
) : MCPMultiCommandTool
{
    // Set command hierarchy - determines CLI command path
    public override CommandGroup[] CommandHierarchy { get; set; } = [
        SharedCommandGroups.YourGroup,
        SharedCommandGroups.YourSubGroup
    ];

    private const string SubCommandName1 = "sub-command-1";
    private const string SubCommandName2 = "sub-command-2";

    private readonly Option<string> fooOption = new(["--foo"], "Foo") { IsRequired = true };
    private readonly Option<string> barOption = new(["--bar"], "Bar");

    protected override List<Command> GetCommands() => [
        new(SubCommandName1, "Analyze something", { fooOption }),
        new(SubCommandName2, "Process something", { fooOption, barOption })
    ];

    public override async Task<CommandResponse> HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        var commandName = ctx.ParseResult.CommandResult.Command.Name;

        if (commandName == SubCommandName1)
        {
            var foo = ctx.ParseResult.GetValueForOption(fooOption);
            return await SubCommand1(foo, ct);
        }

        if (commandName == SubCommandName2)
        {
            var foo = ctx.ParseResult.GetValueForOption(fooOption);
            var bar = ctx.ParseResult.GetValueForOption(barOption);
            return await SubCommand2(foo, bar, ct);
        }
    }

    [McpServerTool(Name = "azsdk_sub_command_1"), Description("Handles first stuff")]
    public async Task<DefaultCommandResponse> SubCommand1(string foo, CancellationToken ct)
    {
        // Implementation
    }

    [McpServerTool(Name = "azsdk_sub_command_2"), Description("Handles second stuff")]
    public async Task<YourResponseType> SubCommand2(string foo, string bar, CancellationToken ct)
    {
        // Implementation
    }
}
```

## Dependency Injection

### Common Service Dependencies

```csharp
public YourTool(
    ILogger<YourTool> logger,                        // Logging - ALWAYS required
    IAzureService azureService,                      // Azure credentials and authentication
    IDevOpsService devopsService,                    // Azure DevOps operations
    IAzureAgentServiceFactory agentServiceFactory,   // AI services factory
    IYourCustomService customService                 // Your domain-specific services
) : base()
```

### Service Usage Guidelines

- **ILogger**: Use for all logging operations (Info, Warning, Error, Debug)
- **IAzureService**: Get Azure credentials, authenticate with Azure services
- **Custom Services**: Implement business logic in separate services, not in tools

### Dependency Guidelines

- **Avoid direct `using` statements** for external dependencies in tools
- Use **injected services only** to maintain testability and loose coupling
- Don't call other Tool classes directly - use shared service or helper classes instead

## Response Handling

Response handling strategies were created with the intent to flexibly handle multiple different types of
callers without the output being too tightly coupled to the tool code.
Calls could be from a CLI invocation in the terminal, tool calls from an MCP client, and potentially more.

NOTE: In CLI mode, the `ResponseError` or `ResponseErrors` property being set on a response object will
default `ExitCode` to `1`. The `ExitCode` property can also be manually overridden.

### Response Class Requirements

A custom response class is not always necessary. It should be defined when the tool needs to:

1. Define formatting rules for complex output data
1. Return structured data that is easier for an LLM to parse
1. Enforce specific fields get set in output
1. Customize error output

All tool response classes must:

1. **Inherit from `Response`** base class
1. **Override `ToString()`** to format properties in a human readable way and return the base `ToString()` method to handle error formatting.
1. **Set JSON serializer attributes** on all properties.

Tools that may have error cases but no need for a custom type should use `DefaultCommandResponse` as
the return type. The `.Result` property takes `object`, but it may not
serialize or stringify correctly if `ToString()` is not overridden.

### Response Class Template

To define a response class, add to [`Azure.Sdk.Tools.Cli/Models/Responses/`](../Azure.Sdk.Tools.Cli/Models/Responses/):

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

// Response class - must inherit from Response
public class YourResponseType : Response
{
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Result { get; set; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    public override string ToString()
    {
        var output = new StringBuilder();
        if (!string.IsNullOrEmpty(Message))
        {
            output.AppendLine($"Message: {Message}");
        }
        if (Result != null)
        {
            output.AppendLine($"Result: {Result?.ToString() ?? "null"}");
        }
        return ToString(output);
    }
}
```

An example usage of `DefaultCommandResponse`:

```csharp
[McpServerTool(Name = "azsdk_hello_world"), Description("Echoes the message back to the client")]
public DefaultCommandResponse EchoSuccess(string message)
{
    try
    {
        logger.LogInformation("Echoing message: {message}", message);
        return new()
        {
            Result = $"RESPONDING TO '{message}' with SUCCESS"
        };
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error occurred while echoing message: {message}", message);
        return new()
        {
            ResponseError = $"Error occurred while processing '{message}': {ex.Message}"
        };
    }
}
```


### Error Handling Patterns

```csharp
// Single error
catch (Exception ex)
{
    logger.LogError(ex, "Error processing {input}", input);
    return new YourResponseType
    {
        ResponseError = $"Failed to process {input}: {ex.Message}"
    };
}

// Multiple errors
var errors = new List<string>();
// ... collect errors
if (errors.Any())
{
    return new YourResponseType
    {
        ResponseErrors = errors
    };
}
```

## Registration and Testing

### Register Your Tool

Add your tool to the `SharedOptions.ToolsList` in [`Commands/SharedOptions.cs`](../Azure.Sdk.Tools.Cli/Commands/SharedOptions.cs):

```csharp
public static readonly List<Type> ToolsList = [
    // ... existing tools
    typeof(YourTool),        // Add your tool here
    // ... more tools
];
```

### Testing Your Tool

From [`[repo root]/tools/azsdk-cli`](../)

**Build the project**:
```
dotnet build
```

**Test CLI functionality**:
```
dotnet run --project Azure.Sdk.Tools.Cli -- yourgroup yourcommand --help
dotnet run --project Azure.Sdk.Tools.Cli -- yourgroup yourcommand input-value --param value
```

**Test MCP functionality**:

Start the MCP server in your MCP client and run the tool via `#my-tool-name some args here`

See [mcp quick start docs](../Azure.Sdk.Tools.Cli/README.md#1-mcp-server-mode)

**Run unit tests**:
```
dotnet test
```

### Example Unit Test

```csharp
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.YourTool;

namespace Azure.Sdk.Tools.Cli.Tests;

internal class YourToolTests
{
    [Test]
    public async Task YourTool_ProcessInput_ReturnsExpectedResult()
    {
        var tool = new YourTool(new TestLogger<YourTool>());
        var result = await tool.YourToolMethod("test-input");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Result, Is.Not.Null);
        Assert.That(result.ToString(), Is.EqualTo("test response"));
    }
}
```

## Required Tool Conventions

### 1. Error Handling
- **Always wrap top-level methods** in try/catch blocks
- **Use specific exception types** when possible for better error messages
- **Log errors with context** before returning error responses

### 2. Logging
- **Use ILogger for all logging** - never `Console.WriteLine` or similar
- **Log at appropriate levels**: Debug, Information, Warning, Error
- **Include relevant context** in log messages (user input, operation details)
- **Don't log sensitive information** (passwords, tokens, PII)
- **Avoid string interpolation**
    - GOOD: `Logger.LogInformation("Received message: {message}", message);`
    - BAD: `Logger.LogInformation($"Received message: {message}");`
    - GOOD: `Logger.LogError(ex, "Error occurred");`
    - BAD: `Logger.LogError($"Error occurred, {ex.Message}");`

### 3. Responses
- **Create/use response classes for both CLI and JSON consumption**
- **Provide meaningful ToString() implementations** in response classes for CLI output

### 4. MCP Server Integration
- **Use descriptive MCP tool names** (snake_case: `azsdk_analyze_pipeline`)
- **Provide clear descriptions** for LLM agents
- **Design parameters for LLM consumption** - clear names and simple parameter types. Be wary of optional parameters that the LLM might eagerly come up with values for.
- **Tool names must be prefixed with azsdk_**

### 5. Command Design Guidelines

- **Use consistent naming**: Commands should follow existing patterns
- **Use kebab-casing**: CLI commands and options should use kebab-case (lowercase and hyphenated)
- **Provide helpful descriptions**: Both for CLI help and MCP discovery
- **Design for both interfaces**: Consider how commands work via CLI and MCP.
    In some cases it makes sense to differ implementations for CLI and MCP mode. A good rule of thumb is that all
    high level scenarios should be invokable from either context.
- **Handle sub-commands properly**: Use command hierarchy and proper routing

### 6. Performance
- **Use async/await properly** for I/O operations
- **Respect cancellation tokens** in long-running operations
- **Dispose resources properly** using `using` statements or try/finally

### 7. Namespace and Organization Rules

- **Correct namespace**: `Azure.Sdk.Tools.Cli.Tools.[YourToolCategory]`
- **File organization**: Group related tools in sub-directories, but keep flat namespace
- **Tool registration**: Always add to `SharedOptions.ToolsList`
- **Dependency patterns**: Use constructor injection, avoid static dependencies

## Common Patterns and Anti-patterns

### ✅ Good Patterns

```csharp
// Good: Proper error handling
[McpServerTool(Name = "azsdk_process_data"), Description("Processes data")]
public async Task<ProcessResponse> ProcessData(string input, CancellationToken ct = default)
{
    try
    {
        logger.LogInformation("Processing data: {input}", input);
        var result = await DoWork(input, ct);
        return new ProcessResponse { Data = result };
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to process data: {input}", input);
        return new ProcessResponse
        {
            ResponseError = $"Failed to process data: {ex.Message}"
        };
    }
}

// Good: Shared logic between CLI and MCP
public override async Task<CommandResponse> HandleCommand(InvocationContext ctx, CancellationToken ct)
{
    var input = ctx.ParseResult.GetValueForArgument(inputArg);
    return await ProcessData(input, ct);
}
```

### ❌ Anti-patterns to Avoid

```csharp
// Bad: No try/catch error handling
[McpServerTool(Name = "azsdk_process_data")]
public ProcessResponse ProcessData(string input)
{
    var result = DoWork(input); // Can throw exceptions
    return new ProcessResponse { Data = result };
}

// Bad: Calling other tools directly
public class BadTool : MCPTool
{
    private readonly AnotherTool anotherTool;

    public BadResponse DoWork()
    {
        return anotherTool.Process(); // Don't do this
    }
}

// Bad: Wrong namespace
namespace Azure.Sdk.Tools.Cli.Tools  // Incorrect, should be Azure.Sdk.Tools.Cli.Tools.YourToolCategory
{
    public class YourTool : MCPTool { }
}

// Bad: Console output in tools
public void DoWork()
{
    Console.WriteLine("Working..."); // Use ILogger instead
}

// Bad: Not returning a response object with ResponseError set
catch (Exception ex)
{
    return $"Exception: {ex.Message}";
}
```
