# New Tool Development Guide

This guide provides comprehensive instructions for creating new Tool classes in the `azsdk-cli` project. These tools serve dual purposes: they can be invoked via the command-line interface (CLI) and exposed through the Model Context Protocol (MCP) server for LLM coding agents.

## Table of Contents

1. [Tool Architecture Overview](#tool-architecture-overview)
2. [Step-by-Step Implementation Guide](#step-by-step-implementation-guide)
3. [Code Examples and Templates](#code-examples-and-templates)
4. [Dependency Injection](#dependency-injection)
5. [Response Handling](#response-handling)
6. [Registration and Testing](#registration-and-testing)
7. [Best Practices](#best-practices)
8. [Common Patterns and Anti-patterns](#common-patterns-and-anti-patterns)

## Tool Architecture Overview

All tools in the azsdk-cli project follow a consistent architecture:

- **Base Class**: All tools inherit from `MCPTool` (defined in `Azure.Sdk.Tools.Cli.Contract`)
- **Namespace**: Tools should be in namespace `Azure.Sdk.Tools.Cli.Tools`
- **Location**: Tool files are organized under `Azure.Sdk.Tools.Cli/Tools/` in logical groupings
- **Attributes**: Tools are decorated with `[McpServerToolType]` for discovery
- **Dual Interface**: Tools support both CLI commands and MCP server methods

### Tool Structure Components

1. **Class Declaration**: Inherits from `MCPTool` with appropriate attributes
2. **Constructor**: Uses dependency injection to receive required services
3. **Command Configuration**: CLI options, arguments, and command hierarchy
4. **CLI Handler**: `GetCommand()` and `HandleCommand()` methods
5. **MCP Methods**: Methods decorated with `[McpServerTool]` for LLM access
6. **Error Handling**: Comprehensive try/catch blocks and response error management

## Step-by-Step Implementation Guide

### Step 1: Determine Tool Placement and Naming

**Questions to Consider:**
- What is the primary function of your tool?
- Does it fit into an existing command group or need a new one?
- What should the CLI command structure look like?

**Naming Conventions:**
- **Class Name**: `{FunctionalName}Tool` (e.g., `LogAnalysisTool`, `PipelineAnalysisTool`)
- **File Location**: `Tools/{Category}/{ToolName}.cs` or `Tools/{ToolName}/{ToolName}.cs`
- **Namespace**: Always `Azure.Sdk.Tools.Cli.Tools` (not subnamespaces)

### Step 2: Define Command Group and Structure

**Command Groups** (defined in `SharedCommandGroups.cs`):
- `AzurePipelines` - Azure DevOps pipeline operations (`azsdk azp`)
- `EngSys` - Engineering system commands (`azsdk eng`)
- `Generators` - File generation commands (`azsdk generators`)
- `Cleanup` - Resource cleanup commands (`azsdk cleanup`)
- `Log` - Log processing commands (`azsdk log`)

**Command Hierarchy Examples:**
```csharp
// Single group: azsdk log analyze
CommandHierarchy = [ SharedCommandGroups.Log ];

// Multiple groups: azsdk eng cleanup agents
CommandHierarchy = [ SharedCommandGroups.EngSys, SharedCommandGroups.Cleanup ];
```

### Step 3: Plan CLI Arguments and Options

**Decision Points:**
- **Arguments**: Required positional parameters (e.g., file paths, IDs)
- **Options**: Optional flags and parameters with default values
- **Sub-commands**: Does your tool need multiple operations?

**Shared Options** (from `SharedOptions.cs`):
- `--output`/`-o`: Output format (plain, json)
- `--debug`: Enable debug logging

### Step 4: Design MCP Methods

**Consider:**
- What methods should be exposed to LLM agents?
- What parameters do they need?
- How should responses be structured?
- What error conditions need handling?

### Step 5: Identify Dependencies

**Common Dependencies:**
- `ILogger<YourTool>` - Always required for logging
- `IOutputService` - Required for CLI output (final results only)
- `IAzureService` - For Azure authentication and credentials
- `IDevOpsService` - For Azure DevOps operations
- Custom service interfaces for your tool's specific needs

## Code Examples and Templates

### Basic Tool Template

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

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Brief description of what this tool does")]
public class YourTool : MCPTool
{
    // Dependencies (injected via constructor)
    private readonly ILogger<YourTool> logger;
    private readonly IOutputService output;

    // CLI Options and Arguments
    private readonly Argument<string> requiredArg = new Argument<string>(
        name: "input",
        description: "Description of required argument"
    ) { Arity = ArgumentArity.ExactlyOne };

    private readonly Option<string> optionalParam = new(["--param", "-p"], "Optional parameter description");
    private readonly Option<bool> flagOption = new(["--flag", "-f"], () => false, "Boolean flag description");

    // Constructor with dependency injection
    public YourTool(
        ILogger<YourTool> logger,
        IOutputService output
        // Add other dependencies as needed
    ) : base()
    {
        this.logger = logger;
        this.output = output;
        
        // Set command hierarchy - determines CLI command path
        CommandHierarchy = [
            SharedCommandGroups.YourGroup  // Results in: azsdk yourgroup yourcommand
        ];
    }

    // CLI Command Configuration
    public override Command GetCommand()
    {
        var command = new Command("yourcommand", "Description for CLI help");
        command.AddArgument(requiredArg);
        command.AddOption(optionalParam);
        command.AddOption(flagOption);
        
        command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        
        return command;
    }

    // CLI Command Handler
    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            // Extract parameters from CLI context
            var input = ctx.ParseResult.GetValueForArgument(requiredArg);
            var param = ctx.ParseResult.GetValueForOption(optionalParam);
            var flag = ctx.ParseResult.GetValueForOption(flagOption);

            // Call your main logic (can be shared with MCP methods)
            var result = await ProcessRequest(input, param, flag, ct);
            
            // Set exit code and output result
            ctx.ExitCode = ExitCode;
            output.Output(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing command");
            SetFailure();
            output.Output(new YourResponseType { ResponseError = ex.Message });
        }
    }

    // MCP Server Method - exposed to LLM agents
    [McpServerTool(Name = "your-tool-method"), Description("Description for LLM agents")]
    public async Task<YourResponseType> ProcessForMcp(string input, string? optionalParam = null, CancellationToken ct = default)
    {
        try
        {
            return await ProcessRequest(input, optionalParam, false, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing MCP request: {input}", input);
            SetFailure();
            return new YourResponseType
            {
                ResponseError = $"Error processing request: {ex.Message}"
            };
        }
    }

    // Shared implementation logic
    private async Task<YourResponseType> ProcessRequest(string input, string? param, bool flag, CancellationToken ct)
    {
        logger.LogInformation("Processing request with input: {input}", input);
        
        // Your implementation logic here
        
        return new YourResponseType
        {
            Result = "Your result data",
            Message = "Success message"
        };
    }
}

// Response class - must inherit from Response
public class YourResponseType : Response
{
    [JsonPropertyName("result")]
    public string? Result { get; set; }
    
    [JsonPropertyName("message")]  
    public string? Message { get; set; }
    
    public override string ToString()
    {
        var output = $"Result: {Result}\nMessage: {Message}";
        return ToString(output); // Calls base method to include errors
    }
}
```

### Complex Tool Example (Multiple Sub-commands)

```csharp
[McpServerToolType, Description("Tool with multiple sub-commands")]
public class ComplexTool : MCPTool
{
    // Command constants
    private const string AnalyzeCommandName = "analyze";
    private const string ProcessCommandName = "process";
    
    public ComplexTool(/* dependencies */) : base()
    {
        // Constructor implementation
    }
    
    public override Command GetCommand()
    {
        // Create parent command
        var parentCommand = new Command("complex", "Complex tool with sub-commands");
        
        // Sub-command 1
        var analyzeCommand = new Command(AnalyzeCommandName, "Analyze something");
        analyzeCommand.AddArgument(/* arguments */);
        analyzeCommand.SetHandler(async ctx => { await HandleAnalyze(ctx, ctx.GetCancellationToken()); });
        
        // Sub-command 2  
        var processCommand = new Command(ProcessCommandName, "Process something");
        processCommand.AddArgument(/* arguments */);
        processCommand.SetHandler(async ctx => { await HandleProcess(ctx, ctx.GetCancellationToken()); });
        
        parentCommand.Add(analyzeCommand);
        parentCommand.Add(processCommand);
        
        return parentCommand;
    }
    
    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        var commandName = ctx.ParseResult.CommandResult.Command.Name;
        
        switch (commandName)
        {
            case AnalyzeCommandName:
                await HandleAnalyze(ctx, ct);
                break;
            case ProcessCommandName:
                await HandleProcess(ctx, ct);
                break;
            default:
                logger.LogError("Unknown command: {command}", commandName);
                SetFailure();
                break;
        }
    }
    
    private async Task HandleAnalyze(InvocationContext ctx, CancellationToken ct)
    {
        // Implementation
    }
    
    private async Task HandleProcess(InvocationContext ctx, CancellationToken ct)
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
    IOutputService output,                           // CLI output - required for CLI commands
    IAzureService azureService,                      // Azure credentials and authentication
    IDevOpsService devopsService,                    // Azure DevOps operations
    IAzureAgentServiceFactory agentServiceFactory,   // AI services factory
    IYourCustomService customService                 // Your domain-specific services
) : base()
```

### Service Usage Guidelines

- **ILogger**: Use for all logging operations (Info, Warning, Error, Debug)
- **IOutputService**: Use ONLY in `GetCommand()` for final CLI output to terminal/MCP client
- **IAzureService**: Get Azure credentials, authenticate with Azure services
- **Custom Services**: Implement business logic in separate services, not in tools

### Dependency Guidelines

- **Avoid direct `using` statements** for external dependencies in tools
- Use **injected services only** to maintain testability and loose coupling
- Don't call other tools directly - use shared services instead
- Keep tools thin - delegate complex logic to services

## Response Handling

### Response Class Requirements

All tool response classes must:

1. **Inherit from `Response`** base class
2. **Populate error fields** on failures: `ResponseError` or `ResponseErrors`
3. **Call `SetFailure()`** on the tool instance for error cases
4. **Use appropriate JSON attributes** for serialization

### Response Class Template

```csharp
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models;

public class YourResponseType : Response
{
    [JsonPropertyName("your_data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public YourDataType? Data { get; set; }
    
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Message { get; set; }
    
    [JsonPropertyName("count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Count { get; set; }

    public override string ToString()
    {
        var output = $"Message: {Message}\nCount: {Count}";
        return ToString(output); // Base method includes error handling
    }
}
```

### Error Handling Patterns

```csharp
// Single error
catch (Exception ex)
{
    logger.LogError(ex, "Error processing {input}", input);
    SetFailure();
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
    SetFailure();
    return new YourResponseType
    {
        ResponseErrors = errors
    };
}
```

## Registration and Testing

### Register Your Tool

Add your tool to the `SharedOptions.ToolsList` in `Commands/SharedOptions.cs`:

```csharp
public static readonly List<Type> ToolsList = [
    // ... existing tools
    typeof(YourTool),        // Add your tool here
    // ... more tools
];
```

### Testing Your Tool

1. **Build the project**:
   ```bash
   cd tools/azsdk-cli
   dotnet build --configuration Debug
   ```

2. **Test CLI functionality**:
   ```bash
   dotnet run --project Azure.Sdk.Tools.Cli -- yourgroup yourcommand --help
   dotnet run --project Azure.Sdk.Tools.Cli -- yourgroup yourcommand input-value --param value
   ```

3. **Test MCP functionality**:
   ```bash
   # Start MCP server
   dotnet run --project Azure.Sdk.Tools.Cli -- mcp
   
   # Test via MCP client or integration tests
   ```

4. **Run unit tests**:
   ```bash
   dotnet test --configuration Debug
   ```

### Example Integration Test

```csharp
[Test]
public async Task YourTool_ProcessInput_ReturnsExpectedResult()
{
    // Arrange
    var logger = new TestLogger<YourTool>();
    var outputService = new Mock<IOutputService>();
    var tool = new YourTool(logger, outputService.Object);

    // Act  
    var result = await tool.ProcessForMcp("test-input");

    // Assert
    Assert.That(result.ResponseError, Is.Null);
    Assert.That(result.Result, Is.Not.Null);
}
```

## Best Practices

### 1. Error Handling
- **Always wrap top-level methods** in try/catch blocks
- **Use specific exception types** when possible for better error messages  
- **Log errors with context** before returning error responses
- **Call `SetFailure()`** on tool instance for all error cases

### 2. Logging
- **Use ILogger for all logging** - never Console.WriteLine or similar
- **Log at appropriate levels**: Debug, Information, Warning, Error
- **Include relevant context** in log messages (user input, operation details)
- **Don't log sensitive information** (passwords, tokens, PII)

### 3. Output
- **Use IOutputService only for final CLI results** - not for progress or debugging
- **Structure output for both CLI and JSON consumption**
- **Provide meaningful ToString() implementations** for CLI output

### 4. MCP Server Integration
- **Use descriptive MCP method names** (kebab-case: `analyze-pipeline`)
- **Provide clear descriptions** for LLM agents
- **Design parameters for LLM consumption** - clear names and optional parameters
- **Consider async operations** - always accept CancellationToken

### 5. Performance
- **Use async/await properly** for I/O operations
- **Respect cancellation tokens** in long-running operations
- **Dispose resources properly** using `using` statements or try/finally

## Common Patterns and Anti-patterns

### ✅ Good Patterns

```csharp
// Good: Proper error handling
[McpServerTool, Description("Processes data")]
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
        SetFailure();
        return new ProcessResponse 
        { 
            ResponseError = $"Failed to process data: {ex.Message}" 
        };
    }
}

// Good: Shared logic between CLI and MCP
public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
{
    var input = ctx.ParseResult.GetValueForArgument(inputArg);
    var result = await ProcessData(input, ct);
    ctx.ExitCode = ExitCode;
    output.Output(result);
}
```

### ❌ Anti-patterns to Avoid

```csharp
// Bad: No error handling
[McpServerTool]
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
namespace Azure.Sdk.Tools.Cli.Tools.YourTool  // Incorrect
{
    public class YourTool : MCPTool { }
}

// Bad: Console output in tools
public void DoWork()
{
    Console.WriteLine("Working..."); // Use ILogger instead
}

// Bad: Not calling SetFailure on errors
catch (Exception ex)
{
    return new Response { ResponseError = ex.Message }; // Missing SetFailure()
}
```

### Namespace and Organization Rules

- **Correct namespace**: `Azure.Sdk.Tools.Cli.Tools`
- **File organization**: Group related tools in subdirectories, but keep flat namespace
- **Tool registration**: Always add to `SharedOptions.ToolsList`
- **Dependency patterns**: Use constructor injection, avoid static dependencies

### Command Design Guidelines

- **Use consistent naming**: Commands should follow existing patterns
- **Provide helpful descriptions**: Both for CLI help and MCP discovery
- **Design for both interfaces**: Consider how commands work via CLI and MCP
- **Handle sub-commands properly**: Use command hierarchy and proper routing

This guide should provide everything needed to create new tools that integrate seamlessly with the azsdk-cli project architecture and patterns.