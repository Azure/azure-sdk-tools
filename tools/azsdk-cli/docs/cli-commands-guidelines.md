# Azure SDK Tools CLI Command Guidelines

This document provides comprehensive guidelines for creating CLI commands in the `Azure.Sdk.Tools.Cli` project. All CLI commands must follow these established patterns and conventions to ensure consistency, maintainability, and proper integration with both CLI and MCP (Model Context Protocol) server functionality.

## Table of Contents

1. [Command Hierarchy](#command-hierarchy)
   - [engsys - Engineering Systems](#1-engsys---engineering-systems)
   - [package - Package Operations](#2-package---package-operations)
   - [github - GitHub Operations](#3-github---github-operations)
   - [release-plan - Release Planning](#4-release-plan---release-planning)
   - [typespec - TypeSpec Operations](#5-typespec---typespec-operations)
   - [tspclient - TypeSpec Client Operations](#6-tspclient---typespec-client-operations)
2. [Namespace Organization](#namespace-organization)
3. [Implementation Requirements](#implementation-requirements)
4. [Command Structure](#command-structure)
5. [Best Practices](#best-practices)
6. [Examples](#examples)
7. [Testing Guidelines](#testing-guidelines)

## Command Hierarchy

All CLI commands must follow a predefined top-level command hierarchy. Commands are organized into the following categories:

### 1. **engsys** - Engineering Systems
**Namespace:** `Azure.Sdk.Tools.Cli.Tools.EngSys`  
**Command Group:** `SharedCommandGroups.EngSys`  
**Verb:** `engsys`

For CLI operations related to engineering systems tooling and services:
- Code ownership validation and manipulation
- Log analysis and processing
- Test analysis and reporting
- Cleanup operations
- Internal engineering workflows

**Examples:**
- `engsys cleanup artifacts`
- `engsys codeowners validate`
- `engsys logs analyze`
- `engsys test-analysis report`

### 2. **package** - Package Operations
**Namespace:** `Azure.Sdk.Tools.Cli.Tools.Package`  
**Command Group:** `SharedCommandGroups.Package`  
**Verb:** `package`  

For operations at the SDK package level. The package group has further sub-grouping for better organization:

#### Core Package Operations:
- Build/compile SDK code
- Generate source code
- Release packages
- Update version information
- Validate packages

#### Sub-Groups:

##### **readme** - README Operations  
For generating and updating README files:
- Generate README files
- Update README content
- Validate README format
- README documentation

##### **sample** - Sample Operations
For generating and updating SDK samples:
- Generate new samples
- Update existing samples
- Validate sample code
- Sample documentation

##### **test** - Test Operations
For creating, updating, and running tests for SDK packages:
- Create new tests
- Update existing tests
- Run tests for specific packages
- Test validation and reporting

**Examples:**
- `package build --package-path ./sdk/storage`
- `package check-release-ready --package-name azure-core --version 1.0.0`
- `package generate --typespec-project ./specification/storage`
- `package readme generate --package-path ./sdk/compute`
- `package readme update --package-path ./sdk/keyvault --section getting-started`
- `pacakge release --package-name azure-core`
- `package sample generate --package-path ./sdk/compute`
- `package sample update --package-path ./sdk/storage --sample-name basic-usage`
- `package show-details`
- `package test generate --package-path ./sdk/storage --test-type <type>`
- `package test run --package-path ./sdk/keyvault --test-suite integration`
- `package validate --package-path ./sdk/keyvault`
- 

### 3. **github** - GitHub Operations
**Namespace:** `Azure.Sdk.Tools.Cli.Tools.GitHub`  
**Command Group:** Custom (no predefined group)  
**Verb:** `github`

For operations related to GitHub:
- Create pull requests
- Check PR status
- Manage labels
- Repository operations

**Examples:**
- `github create-pr --title "Update SDK" --description "Description"`
- `github get-pr-details --pr 123`
- `github labels sync`

### 4. **release-plan** - Release Planning
**Namespace:** `Azure.Sdk.Tools.Cli.Tools.ReleasePlan`  
**Command Group:** Custom (no predefined group)  
**Verb:** `release-plan` 

For release planning and SDK coordination:
- Create release plans
- Update SDK details in release plans
- Link SDK pull requests to release plans
- Get release plan details
- Generate SDK for all languages
- Workflow management

**Examples:**
- `release-plan check-api-readiness --typespec-project ./spec/compute`
- `release-plan generate-sdk --typespec-project ./spec/storage --api-version 2023-01-01`
- `release-plan get --workitem-id 456`
- `release-plan link-sdk-pr --release-plan-id 123 --pr 789`

### 5. **typespec** - TypeSpec Operations
**Namespace:** `Azure.Sdk.Tools.Cli.Tools.TypeSpec`  
**Command Group:** `SharedCommandGroups.TypeSpec`  
**Verb:** `tsp`  

For TypeSpec-related operations:
- Create new TypeSpec projects
- Convert Swagger to TypeSpec
- Validate TypeSpec projects
- Common TypeSpec workflows

**Examples:**
- `tsp convert --swagger-file ./swagger.json`
- `tsp init --name MyService`
- `tsp validate --project-path ./typespec`

### 6. **tspclient** - TypeSpec Client Operations
**Namespace:** `Azure.Sdk.Tools.Cli.Tools.TypeSpec` (may have dedicated namespace)  
**Command Group:** `SharedCommandGroups.TypeSpec` or custom  
**Verb:** `tspclient` or integrated into `tsp`

For TypeSpec client-specific operations:
- Run TypeSpec client operations
- Update TypeSpec client configurations
- Client generation workflows

**Examples:**
- `tsp-client generate --output-dir ./generated`
- `tsp-client update --config-path ./tspconfig.yaml`

## Namespace Organization

### Directory Structure
```
Tools/
├── EngSys/           # Engineering systems commands
├── Package/          # Package-level operations
├── GitHub/           # GitHub-related operations  
├── ReleasePlan/      # Release planning operations
├── TypeSpec/         # TypeSpec operations
└── Example/          # Example implementations (DEBUG only)
```

### Namespace Conventions
- Use descriptive namespaces that match the directory structure
- Follow the pattern: `Azure.Sdk.Tools.Cli.Tools.{Category}`
- Each tool class should be in its appropriate namespace directory

## Implementation Requirements

### 1. Base Class Implementation
All CLI tools must inherit from `MCPTool`:

```csharp
[McpServerToolType]
[Description("Description of what this tool does")]
public class YourTool : MCPTool
{
    // Implementation
}
```

### 2. Required Attributes
- `[McpServerToolType]`: Marks the class as an MCP server tool
- `[Description("...")]`: Provides a description for the tool

### 3. Required Method Overrides
```csharp
public override Command GetCommand()
{
    // Return the CLI command configuration
}

public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
{
    // Handle command execution
}
```

### 4. Constructor Pattern
```csharp
public YourTool(
    IDependency1 dependency1,
    IDependency2 dependency2,
    ILogger<YourTool> logger,
    IOutputHelper output) : base()
{
    // Initialize dependencies
    // Set CommandHierarchy if using command groups
    CommandHierarchy = [ SharedCommandGroups.YourGroup ];
}
```

### 5. MCP Tool Methods
For MCP server functionality, add methods with:
```csharp
[McpServerTool(Name = "tool_name")]
[Description("Tool description")]
public async Task<string> YourMcpMethod()
{
    // Implementation
    // Always return formatted output using _output.Format()
}
```

## Command Structure

### 1. Command Definition
```csharp
public override Command GetCommand()
{
    var command = new Command("command-name", "Command description");
    
    // Add options
    command.AddOption(SharedOptions.CommonOption);
    command.AddOption(customOption);
    
    // Add sub-commands if needed
    var subCommand = new Command("sub-command", "Sub-command description");
    command.AddCommand(subCommand);
    
    // Set handler
    command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
    
    return command;
}
```

### 2. Option Definition
```csharp
private readonly Option<string> customOption = new(
    ["--custom", "-c"], 
    "Description of the custom option"
) { IsRequired = false };
```

### 3. Command Handling
```csharp
public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
{
    try
    {
        var command = ctx.ParseResult.CommandResult.Command.Name;
        var optionValue = ctx.ParseResult.GetValueForOption(customOption);
        
        // Perform command logic
        var result = await PerformOperation(optionValue, ct);
        
        // Set exit code
        ctx.ExitCode = ExitCode;
        
        // Output result
        _output.Output(result);
    }
    catch (Exception ex)
    {
        SetFailure();
        _logger.LogError(ex, "Error executing command");
        _output.Output(_output.Format($"Error: {ex.Message}"));
    }
}
```

## Examples

### Basic Package Tool Example
```csharp
namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    [McpServerToolType]
    [Description("Tool for validating SDK packages")]
    public class PackageValidationTool : MCPTool
    {
        private const string ValidateCommandName = "validate";
        private readonly IOutputHelper _output;
        private readonly ILogger<PackageValidationTool> _logger;
        
        public PackageValidationTool(
            ILogger<PackageValidationTool> logger,
            IOutputHelper output) : base()
        {
            _logger = logger;
            _output = output;
            CommandHierarchy = [ SharedCommandGroups.Package ];
        }
        
        public override Command GetCommand()
        {
            var command = new Command(ValidateCommandName, "Validates an SDK package");
            command.AddOption(SharedOptions.PackagePath);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }
        
        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);
                var result = await ValidatePackage(packagePath, ct);
                _output.Output(result);
            }
            catch (Exception ex)
            {
                SetFailure();
                _logger.LogError(ex, "Package validation failed");
                _output.Output(_output.Format($"Validation failed: {ex.Message}"));
            }
        }
        
        private async Task<string> ValidatePackage(string packagePath, CancellationToken ct)
        {
            // Implementation
            return _output.Format("Package validation completed successfully");
        }
    }
}
```

### GitHub Tool Example
```csharp
namespace Azure.Sdk.Tools.Cli.Tools.GitHub
{
    [McpServerToolType]
    [Description("Tool for GitHub repository operations")]
    public class RepositoryTool : MCPTool
    {
        private const string ListBranchesCommandName = "list-branches";
        
        private readonly IGitHubService _githubService;
        private readonly IOutputHelper _output;
        private readonly ILogger<RepositoryTool> _logger;
        
        private readonly Option<string> repoOption = new(
            ["--repo", "-r"], 
            "Repository name (owner/repo)"
        ) { IsRequired = true };
        
        public RepositoryTool(
            IGitHubService githubService,
            ILogger<RepositoryTool> logger,
            IOutputHelper output) : base()
        {
            _githubService = githubService;
            _logger = logger;
            _output = output;
        }
        
        public override Command GetCommand()
        {
            var command = new Command(ListBranchesCommandName, "Lists repository branches");
            command.AddOption(repoOption);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }
        
        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                var repo = ctx.ParseResult.GetValueForOption(repoOption);
                var branches = await _githubService.GetBranchesAsync(repo, ct);
                _output.Output(_output.Format($"Found {branches.Count} branches"));
            }
            catch (Exception ex)
            {
                SetFailure();
                _logger.LogError(ex, "Failed to list branches");
                _output.Output(_output.Format($"Error: {ex.Message}"));
            }
        }
    }
}
```

## Command Registration

When creating new tools, ensure they are registered in:

1. **SharedOptions.ToolsList** - Add your tool type to the list
2. **Service Registration** - Ensure dependencies are registered in the DI container
3. **Command Groups** - Add new command groups to `SharedCommandGroups` if needed

## Conclusion

Following these guidelines ensures that all CLI commands in the Azure SDK Tools project maintain consistency, reliability, and proper integration with both CLI and MCP server functionality. Always refer to existing implementations for patterns and best practices, and ensure thorough testing of new commands.

For questions or clarifications, refer to existing tool implementations in the `Tools` directory or consult the project maintainers.