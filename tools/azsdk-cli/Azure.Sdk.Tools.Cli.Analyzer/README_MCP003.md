# Tool Return Type Analyzer (MCP003)

## Overview

The `EnforceToolsReturnTypesAnalyzer` ensures that all public non-static methods in classes within the `Azure.Sdk.Tools.Cli.Tools` namespace return only approved types at compile time.

## Rule: MCP003

**Title**: Tool methods must return Response types, built-in value types, or string

**Description**: Method '{0}' in Tools namespace must return a class implementing Response, a built-in value type, or string. Current return type: '{1}'.

## Allowed Return Types

### 1. Classes implementing `Azure.Sdk.Tools.Cli.Models.Response`
- `ValidationResponse`
- `DefaultCommandResponse` 
- `DownloadResponse`
- `FailedTestRunResponse`
- `LogAnalysisResponse`
- `ObjectCommandResponse`
- `PackageResponse`
- `SDKWorkflowResponse`
- `SdkReleaseResponse`
- Custom Response classes

### 2. C# Built-in Value Types
- `bool`
- `char`, `byte`, `sbyte`
- `short`, `ushort`, `int`, `uint`, `long`, `ulong`
- `float`, `double`, `decimal`
- `string`
- `IntPtr`, `UIntPtr`
- `DateTime`

### 3. Async Variations
- `Task<T>` where T is any allowed type above
- `ValueTask<T>` where T is any allowed type above  
- `Task` (void async methods)

### 4. Framework Methods (Excluded)
- `GetCommand()` - Command line interface method
- `HandleCommand()` - Command execution method
- Abstract, virtual, or override methods

## Examples

### ✅ Valid Return Types
```csharp
// Response types
public async Task<ValidationResponse> ValidateAsync() { }
public ValidationResponse Validate() { }

// Built-in types  
public async Task<string> GetMessageAsync() { }
public async Task<int> GetCountAsync() { }
public async Task<bool> IsValidAsync() { }

// Void async
public async Task ProcessAsync() { }
```

### ❌ Invalid Return Types
```csharp
// Collections - should be wrapped in Response type
public async Task<List<int>> GetIds() { }
public async Task<List<ValidationResponse>> GetResults() { }
public IList<string> GetMessages() { }

// Custom types that don't inherit from Response
public async Task<CustomObject> GetCustom() { }
public Dictionary<string, object> GetData() { }
```

## Migration Guide

When you encounter MCP003 violations:

1. **For collections**: Create a Response type that contains the collection
   ```csharp
   // Instead of:
   public async Task<List<int>> GetIds() { }
   
   // Use:
   public async Task<IdsResponse> GetIds() { }
   
   public class IdsResponse : Response 
   {
       public List<int> Ids { get; set; }
   }
   ```

2. **For custom objects**: Make them inherit from Response or wrap in Response
   ```csharp
   // Instead of:
   public async Task<CustomData> GetData() { }
   
   // Option 1: Make CustomData inherit Response
   public class CustomData : Response { }
   
   // Option 2: Wrap in Response type  
   public async Task<CustomDataResponse> GetData() { }
   ```

## Implementation Details

- **Analyzer ID**: MCP003
- **Severity**: Error
- **Category**: Design
- **Scope**: Public non-static methods in `Azure.Sdk.Tools.Cli.Tools` namespace
- **Framework Integration**: Automatically runs during compilation via analyzer reference