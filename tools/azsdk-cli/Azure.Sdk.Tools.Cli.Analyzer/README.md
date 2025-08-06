# Naming Convention Analyzers

This directory contains analyzers that enforce naming conventions for the azsdk-cli project.

## Analyzers

### MCP003 - CLI Command Naming Convention
Enforces kebab-case naming for CLI commands.

**Valid examples:**
- `new Command("hello-world", "description")`
- `new Command("test", "description")`
- `new Command("api-spec", "description")`

**Invalid examples:**
- `new Command("helloWorld", "description")` - camelCase
- `new Command("Hello", "description")` - PascalCase
- `new Command("hello_world", "description")` - snake_case
- `new Command("hello-World", "description")` - mixed case

### MCP004 - Missing MCP Server Tool Name
Enforces that McpServerTool attributes specify a Name property.

**Invalid examples:**
- `[McpServerTool]` - missing Name property
- `[McpServerTool()]` - empty attribute

### MCP005 - MCP Server Tool Naming Convention
Enforces snake_case naming for MCP server tool names.

**Valid examples:**
- `[McpServerTool(Name = "hello_world")]`
- `[McpServerTool(Name = "test_tool")]`
- `[McpServerTool(Name = "api_validator")]`

**Invalid examples:**
- `[McpServerTool(Name = "helloWorld")]` - camelCase
- `[McpServerTool(Name = "Hello_World")]` - PascalCase  
- `[McpServerTool(Name = "hello-world")]` - kebab-case
- `[McpServerTool(Name = "HelloWorld")]` - PascalCase

## Gradual Rollout

Due to existing code violations, these analyzers are currently **disabled by default** in the main project. This allows for a gradual migration approach.

### Current Status
- ✅ Analyzers implemented and working correctly
- ✅ Build succeeds with analyzers temporarily disabled 
- ✅ Example violations fixed in HelloWorldTool and ReleaseReadinessTool
- 📋 ~40 violations remain in existing codebase

### Enabling Analyzers for New Code

To enable the analyzers for a specific file (recommended for new files), remove the analyzer warnings from the NoWarn list just for that file by adding this at the top:

```csharp
// Enable naming convention analyzers for this file
#pragma warning enable MCP003 // CLI command names must follow kebab-case convention
#pragma warning enable MCP004 // McpServerTool attribute must specify a Name property  
#pragma warning enable MCP005 // McpServerTool Name must follow snake_case convention
```

### Disabling Analyzers for Existing Code

For existing files during migration, you can disable specific analyzers:

```csharp
#pragma warning disable MCP003 // CLI command names must follow kebab-case convention
#pragma warning disable MCP004 // McpServerTool attribute must specify a Name property  
#pragma warning disable MCP005 // McpServerTool Name must follow snake_case convention
```

### Project-wide Enablement

When ready to enforce project-wide, remove the warnings from the main `.csproj` file:

```xml
<!-- In Azure.Sdk.Tools.Cli.csproj, remove MCP003;MCP004;MCP005 from NoWarn -->
<PropertyGroup>
  <NoWarn>ASP0000;CS8603;CS8618;CS8625;CS8604</NoWarn> 
</PropertyGroup>
```

## Example Usage

See `Examples/NamingConventionDemo.cs` for examples of correct and incorrect naming patterns.