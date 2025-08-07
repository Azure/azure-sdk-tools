# Tool Exception Handling Analyzer (MCP001)

# Tool Service Registration Analyzer (MCP002)

# Tool Return Type Analyzer (MCP003)

## Overview

The `EnforceToolsReturnTypesAnalyzer` ensures that all public non-static methods in classes within
the `Azure.Sdk.Tools.Cli.Tools` namespace return only approved types at compile time.

## Rule: MCP003

**Title**: Tool methods must return Response types, built-in value types, or string

This excludes inherited methods `GetCommand`, `HandleCommand`, etc.

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