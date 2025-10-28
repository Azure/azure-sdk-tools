# Tool Exception Handling Analyzer (MCP001)

## Overview

The `EnforceToolsExceptionHandlingAnalyzer` ensures that all methods decorated with the `[McpServerTool]` attribute properly wrap their entire body in try/catch blocks with proper exception handling, with the exception of top-level variable declarations.

Unhandled exceptions in MCP mode prevent the client from logging tool responses correctly as the error information is not transmitted through the protocol. Currently there is no middleware support in the MCP C# SDK that could intercept unhandled exceptions. See https://github.com/modelcontextprotocol/csharp-sdk/issues/267

## Requirements

1. **Try/catch wrapping**: The entire method body must be within a try statement
2. **Exception type**: Must catch `System.Exception` (not specific exception types)
3. **Variable declarations**: Local variable declarations are allowed outside the try block
4. **SetFailure() call**: The catch block should call `SetFailure()` to mark the tool as failed

## Migration Guide

When you encounter MCP001 violations:

```csharp
// ❌ Incorrect - no try/catch
[McpServerTool]
public async Task<Response> ProcessData(string myArg)
{
    var parsedArg = myArg.Trim(" ");
    var result = await DoSomething(parsedArg);
    return new Response { Data = result };
}

// ✅ Correct - proper try/catch structure
[McpServerTool]
public async Task<Response> ProcessData(string myArg)
{
    // Variables are allowed to be defined outside the try/catch
    // if they need to be referenced in the catch block
    var parsedArg = myArg.Trim(" ");

    try
    {
        var result = await DoSomething(parsedArg);
        return new Response { Data = result };
    }
    catch (Exception ex)
    {
        SetFailure();
        logger.LogError(ex, "Error processing data");
        return new Response { ResponseError = $"Error processing data for {parsedArg}: {ex.Message}" };
    }
}
```

# Tool Service Registration Analyzer (MCP002)

## Overview

The `EnforceToolsListAnalyzer` ensures that every class inheriting from `MCPToolBase` is properly registered in the `SharedOptions.ToolsList` static list, otherwise they will not be loaded at startup.

All non-abstract classes that inherit from `Azure.Sdk.Tools.Cli.Contract.MCPTool` must be included as `typeof(ClassName)` entries in the `SharedOptions.ToolsList` static field (`Azure.Sdk.Tools.Cli/Commands/SharedOptions.cs`).

## Requirements

1. **Registration**: Every `MCPTool` implementation must appear in `SharedOptions.ToolsList`
2. **Typeof syntax**: Tools must be registered using `typeof(YourToolClass)`
3. **Non-abstract only**: Only concrete (non-abstract) classes are validated
4. **Compile-time check**: This validation happens at compilation end

## Migration Guide

When you encounter MCP002 violations:

```csharp
// 1. Tool implementation
public class MyCustomTool : MCPTool
{
    protected override Command GetCommand() { /* implementation */ }
    public override Task HandleCommand(InvocationContext ctx, CancellationToken ct) { /* implementation */ }
}

// 2. Add it to SharedOptions.ToolsList in Azure.Sdk.Tools.Cli/Commands/SharedOptions.cs
public static readonly List<Type> ToolsList = [
    typeof(ExistingTool1),
    typeof(ExistingTool2),
    typeof(MyCustomTool),  // ← Add your new tool here
    // ... other tools
];
```

# Tool Return Type Analyzer (MCP003)

## Overview

The `EnforceToolsReturnTypesAnalyzer` ensures that all public non-static methods in classes within
the `Azure.Sdk.Tools.Cli.Tools` namespace return only approved types at compile time.

This excludes inherited methods `GetCommand`, `GetCommands`, `HandleCommand`, etc.

## Allowed Return Types

1. Classes implementing `Azure.Sdk.Tools.Cli.Models.Response`
1. `string`
1. Primitive types (`int`, `bool`, etc.)
1. `IEnumerable<T>` of any of the above
1. `Task` (for `Task<T>` T must be any of the above)
1. `void`

## Migration Guide

When you encounter MCP003 violations:

**For custom objects**: Make them inherit from Response or wrap in Response
```csharp
// Instead of:
public async Task<CustomData> GetData() { }

// Option 1: Make CustomData inherit Response
public class CustomData : Response { }

// Option 2: Wrap in Response type
public async Task<CustomDataResponse> GetData() { }
```

Exceptions are not handled from top-level tool methods.
To bubble errors up in a way that can be formatted for supported callers (CLI, MCP, etc.),
return the custom type and set the inherited `ResponseError` or `ResponseErrors` property:

```csharp
try
{
   // Tool business logic here
}
catch (Exception ex)
{
   SetFailure();
   logger.LogError(ex, "Error running tool");
   return new CustomData {
      ResponseError: $"Error running tool: {ex.Message}";
   }
}
```


# CLI Command Naming Convention Analyzer (MCP004)

## Overview

`kebab-case` style naming is required for CLI command names.

**Valid examples:**
- `new Command("hello-world", "description")`
- `new Command("test", "description")`
- `new Command("api-spec", "description")`

**Invalid examples:**
- `new Command("helloWorld", "description")` - camelCase
- `new Command("Hello", "description")` - PascalCase
- `new Command("hello_world", "description")` - `snake_case`
- `new Command("hello-World", "description")` - mixed case


# CLI Option Naming Convention (MCP005)

## Overview

`kebab-case` style naming is required for CLI option names (only long options starting with `--`).

**Valid examples:**
- `new Option<string>(["--log-id"], "description")`
- `new Option<string>(["--project", "-p"], "description")` - kebab-case with short option
- `new Option<string>(["-v"], "description")` - short option only (not validated)

**Invalid examples:**
- `new Option<string>(["--logId"], "description")` - camelCase
- `new Option<string>(["--project_name"], "description")` - `snake_case`
- `new Option<string>(["--ProjectName"], "description")` - PascalCase


# MCP Server Tool Name Required (MCP006)

## Overview

Methods with the `[McpServerTool]` attribute must specify a Name property.

**Invalid examples:**
- `[McpServerTool]` - missing Name property
- `[McpServerTool()]` - empty attribute


# MCP Server Tool Naming Convention (MCP007)

## Overview

`snake_case` style naming is required for MCP server tool names.

**Valid examples:**
- `[McpServerTool(Name = "hello_world")]`
- `[McpServerTool(Name = "test_tool")]`
- `[McpServerTool(Name = "api_validator")]`

**Invalid examples:**
- `[McpServerTool(Name = "helloWorld")]` - camelCase
- `[McpServerTool(Name = "Hello_World")]` - PascalCase
- `[McpServerTool(Name = "hello-world")]` - kebab-case
- `[McpServerTool(Name = "HelloWorld")]` - PascalCase

# MCP Server Tool Name Prefix (MCP008)

## Overview

All MCP server tool names must start with the prefix `azsdk_`. This ensures clear ownership and avoids name collisions across MCP server tool lists.

**Valid examples:**
- `[McpServerTool(Name = "azsdk_hello_world")]`
- `[McpServerTool(Name = "azsdk_generate_sdk")]`

**Invalid examples:**
- `[McpServerTool(Name = "hello_world")]`
- `[McpServerTool(Name = "generate_sdk")]`
