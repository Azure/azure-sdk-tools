# New Tool Development Guide

This guide provides comprehensive instructions for creating new Tool classes in the `azsdk-cli` project. These tools serve dual purposes: they can be invoked via the command-line interface (CLI) and exposed through the Model Context Protocol (MCP) server for LLM coding agents.

New tools can be created with copilot chat/agent:

```
Help me create a new tool using #new-tool.md as a reference
```

## Table of Contents

   * [Tool Architecture Overview](#tool-architecture-overview)
   * [Step-by-Step Implementation Guide](#step-by-step-implementation-guide)
   * [CLI Command Hierarchy](#cli-command-hierarchy)
   * [Code Examples and Templates](#code-examples-and-templates)
   * [Dependency Injection](#dependency-injection)
   * [Response Handling](#response-handling)
   * [Prompt Template System](#prompt-template-system)
   * [Registration and Testing](#registration-and-testing)
   * [Tool Discoverability Testing](#add-test-prompts-for-tool-discoverability)
   * [Required Tool Conventions](#required-tool-conventions)
   * [Common Patterns and Anti-patterns](#common-patterns-and-anti-patterns)

## Tool Architecture Overview

All tools in the azsdk-cli project follow a consistent architecture:

- **Location**: Tool files are organized under [`Azure.Sdk.Tools.Cli/Tools/`](../Azure.Sdk.Tools.Cli/Tools/{Category}/) in logical categories
- **Namespace**: Tools should be in namespace `Azure.Sdk.Tools.Cli.Tools.{Category}`
- **Base Class**: If the purpose of the tool is to run an operation at package or language level, the tool must inherit from [`LanguageMcpTool`](../Azure.Sdk.Tools.Cli/Tools/Core/LanguageMcpTool.cs) or [`LanguageMultiCommandTool`](../Azure.Sdk.Tools.Cli/Tools/Core/LanguageMultiCommandTool.cs).
  Otherwise, all other tools must inherit from [`MCPTool`](../Azure.Sdk.Tools.Cli/Tools/Core/MCPTool.cs) or [`MCPMultiCommandTool`](../Azure.Sdk.Tools.Cli/Tools/Core/MCPMultiCommandTool.cs).
- **Attributes**: Tools are decorated with `[McpServerToolType]` for discovery
- **Dual Interface**: Tools support both CLI commands and MCP server methods

### Tool Structure Components

1. **Class Declaration**: Inherits from `MCPTool` or `MCPMultiCommandTool` with appropriate attributes
2. **Constructor**: Uses dependency injection to receive required services
3. **Command Configuration**: CLI options, arguments, and command hierarchy
4. **CLI Handler**: `GetCommand()` (for `MCPTool`) or `GetCommands()` (for `MCPMultiCommandTool`) with a `HandleCommand(ParseResult parseResult, CancellationToken ct)` implementation
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
- `Samples` - Sample generation and management commands (`azsdk samples`)
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

## CLI command hierarchy

Refer to [CLI command hierarchy](cli-commands-guidelines.md) for guidelines on CLI command structure.

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
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
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
    private readonly Argument<string> requiredArg = new("input")
    {
        Description = "Description of required argument",
        Arity = ArgumentArity.ExactlyOne,
    };

    private readonly Option<string> optionalParam = new("--param", "-p")
    {
        Description = "Optional parameter description",
        Required = false,
    };

    private readonly Option<bool> flagOption = new("--flag", "-f")
    {
        Description = "Boolean flag description",
        DefaultValueFactory = _ => false,
    };

    // CLI Command Configuration
    protected override Command GetCommand() => new("your-command", "Description for CLI help")
    {
        requiredArg, optionalParam, flagOption
    }

    // CLI Command Handler
    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        // Extract parameters from CLI context
        var input = parseResult.GetValue(requiredArg);
        var param = parseResult.GetValue(optionalParam);
        var flag = parseResult.GetValue(flagOption);

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

    private readonly Option<string> fooOption = new("--foo")
    {
        Description = "Foo",
        Required = true,
    };

    private readonly Option<string> barOption = new("--bar")
    {
        Description = "Bar",
        Required = false,
    };

    private readonly Option<string> bazOption = new("--baz")
    {
        Description = "Baz",
        Required = false,
    };

    protected override List<Command> GetCommands() =>
    [
        new(SubCommandName1, "Analyze something") { fooOption },
        new(SubCommandName2, "Process something")
        {
            fooOption, barOption, bazOption
        }
    ]

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var commandName = parseResult.CommandResult.Command.Name;

        if (commandName == SubCommandName1)
        {
            var foo = parseResult.GetValue(fooOption);
            return await SubCommand1(foo, ct);
        }

        if (commandName == SubCommandName2)
        {
            var foo = parseResult.GetValue(fooOption);
            var bar = parseResult.GetValue(barOption);
            return await SubCommand2(foo, bar, ct);
        }

        return new DefaultCommandResponse { ResponseError = $"Unknown command: '{commandName}'" };
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

A custom response class is not always necessary. It should be defined if the tool is under a specific category or when the tool needs to:

1. Define formatting rules for complex output data
1. Return structured data that is easier for an LLM to parse
1. Enforce specific fields get set in output
1. Customize error output

#### Response base class for custom tool response

We have a predefined set of base classes for custom tool response to ensure all required basic properties are set in the responses.
This allows us to classify the tool and command usage in telemetry.

1. **PackageResponseBase**
If the goal of MCP tool/command is to run operations at a package level or language repo level then MCP tool response must use a response class derived from `PackageResponseBase`
and these custom response classes are defined in [package responses](../Azure.Sdk.Tools.Cli/Models/Responses/Package).

1. **TypeSpecBaseResponse**
If the goal of MCP tool is to run operations at TypeSpec project level then a custom response must be derived from `TypeSpecBaseResponse`.
There are a few predefined custom responses for TypeSpec operations defined in [TypeSpec](../Azure.Sdk.Tools.Cli/Models/Responses/TypeSpec)

1. **ReleasePlanBaseResponse**
If the goal of MCP tool is to run operations at a release plan level then a custom response must be derived from `ReleasePlanBaseResponse`.
There are a few predefined custom responses for release plan operations defined in [ReleasePlan](../Azure.Sdk.Tools.Cli/Models/Responses/ReleasePlan)

All tool response classes must:

1. **Inherit from `CommandResponse`** base class if not derived from above mentioned custom base class.
1. **Override `Format()`** to format properties in a human readable way.
1. **Set JSON serializer attributes** on all properties.

Tools that may have error cases but no need for a custom type should use `DefaultCommandResponse` as
the return type. The `.Result` property takes `object`, but must override `Format()` to serialize/stringify
the value.

#### Setting Required Telemetry Information in Tool Responses

To properly tag tool calls in telemetry, tool responses must include the following information when applicable:

1. **Package Name**
   - **Required when**: Tool is triggered/operated at a package level
   - **How to set**: Set the `PackageName` property in responses derived from `PackageResponseBase`
   - **Purpose**: Identifies which package the tool operation was performed on

2. **Language**
   - **Required when**: Tool/command is specifically run for an SDK language
   - **How to set**: Set the `Language` property in responses derived from `PackageResponseBase` or use the `SetLanguage()` method
   - **Purpose**: Identifies which SDK language the tool operation was performed for

3. **TypeSpec Project Path**
   - **Required when**: TypeSpec path is known to the tool
   - **How to set**: Set the `TypeSpecProject` property with the relative path to TypeSpec project root in responses derived from `PackageResponseBase`, `TypeSpecBaseResponse`, or `ReleasePlanBaseResponse`
   - **Purpose**: Links the operation to a specific TypeSpec project

4. **Package Type**
   - **Required when**: Tool call is at a package level or TypeSpec project level
   - **How to set**: Set the `PackageType` property to `SdkType.Management` or `SdkType.Dataplane` in responses derived from `PackageResponseBase`, `TypeSpecBaseResponse`, or `ReleasePlanBaseResponse`, or use the `SetPackageType()` method
   - **Purpose**: Classifies the package as management plane or data plane
   - **Note**: Package type is known for TypeSpec projects and SDK packages

5. **Tool Operation Status**
   - **Required when**: Tool operation encounters an error or failure
   - **How to set**: Set the `ResponseError` property (for a single error) or `ResponseErrors` property (for multiple errors) in any response derived from `CommandResponse`
   - **Purpose**: Marks a tool operation as failed in telemetry, which will be included in the failed tool call list
   - **Note**: Setting either `ResponseError` or `ResponseErrors` automatically sets `OperationStatus` to `Status.Failed`


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

    protected override string Format()
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
        return output.ToString();
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

## Prompt Template System

For tools using AI models (microagents, LLMs), use the standardized Prompt Template System instead of ad-hoc string formatting. This provides consistent structure, built-in safety guidelines, and Microsoft policy compliance.

### Quick Start

**Use built-in templates directly:**
```csharp
// Common scenarios - spelling, README, log analysis
var prompt = PromptTemplates.GetMicroagentSpellingFixPrompt(cspellOutput, "Azure SDK for .NET");
var prompt = PromptTemplates.GetReadMeGenerationPrompt(templateContent, serviceDocUrl, packagePath);
var prompt = PromptTemplates.GetLogAnalysisPrompt(logContent, "Azure DevOps Pipeline", "json");
```

### Creating Custom Templates

For specialized prompts, inherit from `BasePromptTemplate`:

```csharp
public class MyCustomTemplate : BasePromptTemplate
{
    public override string TemplateId => "my-custom-analysis";
    public override string Version => "1.0.0";
    public override string Description => "Analyzes custom data format";

    /// <summary>
    /// Builds a custom analysis prompt with strongly typed parameters.
    /// </summary>
    /// <param name="inputData">The data to analyze</param>
    /// <param name="analysisType">Type of analysis to perform</param>
    /// <returns>Complete structured prompt for custom analysis</returns>
    public override string BuildPrompt(string inputData, string analysisType = "general")
    {
        var taskInstructions = $"""
        You are a data analysis assistant.

        Analyze the following data using {analysisType} analysis techniques:

        **Data to Analyze:**
        ```
        {inputData}
        ```
        """;

        return BuildStructuredPrompt(taskInstructions);
    }
}

// Use the template directly
var template = new MyCustomTemplate();
var prompt = template.BuildPrompt(analysisData, "statistical");

// Use with microagent
var microagent = new Microagent<AnalysisResult>
{
    Instructions = prompt,
    Model = "gpt-4",
    MaxToolCalls = 10
};
```

All templates automatically include safety guidelines, Microsoft policy compliance, and structured output requirements.

### Template System Guidelines

**Best Practices:**

* Use built-in templates when possible (spelling, README, log analysis)
* Create custom templates for specialized domains
* Always include safety guidelines and validation
* Design templates for reusability across similar use cases
* Use clear, descriptive parameter names for LLM consumption

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

### Add Test Prompts for Tool Discoverability

When creating a new tool, you **must** add test prompts to validate that LLM agents can discover your tool from natural language queries. This ensures your tool's description is effective for embedding-based tool matching.

**Add 2-3 prompt variations** to [`Azure.Sdk.Tools.Cli.Evaluations/TestData/TestPrompts.json`](../Azure.Sdk.Tools.Cli.Evaluations/TestData/TestPrompts.json):

```json
{ "toolName": "azsdk_your_tool_method", "prompt": "Natural language query that should match your tool", "category": "all" },
{ "toolName": "azsdk_your_tool_method", "prompt": "Alternative phrasing users might use", "category": "all" },
{ "toolName": "azsdk_your_tool_method", "prompt": "Another variation of the request", "category": "all" }
```

**Guidelines for test prompts:**
- Write prompts as users would naturally phrase them (not technical descriptions)
- Include variations: questions, commands, different terminology
- Use `"category": "all"` for tools that work in any repository
- Use `"category": "azure-rest-api-specs"` for tools specific to the specs repository

**Run the discoverability tests:**
```bash
# Test all prompts match their expected tools
dotnet test Azure.Sdk.Tools.Cli.Evaluations --filter "Name~Evaluate_PromptToToolMatch"

# Verify your tool has test prompts (will fail if missing)
dotnet test Azure.Sdk.Tools.Cli.Evaluations --filter "Name~AllToolsHaveTestPrompts"
```

**If tests fail**, your tool description may need improvement. The test output shows:
- Current ranking of your tool for the prompt
- Confidence score (must be ≥40%)
- Your tool's description (to help identify what needs improvement)

**Tips for good tool descriptions:**
- Include action verbs users would say: "analyze", "check", "create", "update"
- Mention the domain/context: "pipeline", "SDK", "TypeSpec", "release plan"
- Be specific about what the tool does, not just technical implementation details

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
- **Provide meaningful Format() implementations** in response classes for CLI output

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
public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
{
    var input = parseResult.GetValue(inputArg);
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