# Creating Benchmark Scenarios

This guide explains how to create new benchmark scenarios for testing AI agent capabilities.

## Overview

A **scenario** is a single benchmark task that tests whether an AI agent can complete a specific Azure SDK task correctly. Each scenario defines:

- **Repository Context**: Which GitHub repository the agent operates in
- **Prompt**: The task for the agent to complete
- **Setup**: Optional pre-execution file modifications
- **Validation**: How we determine pass/fail (coming soon)

Scenarios are defined as C# classes that extend `BenchmarkScenario`. This provides type safety, IDE support, and flexibility for complex setup logic.

## Creating a Scenario

### Step 1: Create a New Class

Create a new class in the `Scenarios/` folder that extends `BenchmarkScenario`:

```csharp
using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

public class MyScenario : BenchmarkScenario
{
    // Required properties go here
}
```

### Step 2: Implement Required Properties

Every scenario **must** implement these abstract properties:

| Property | Type         | Description                                                   |
| -------- | ------------ | ------------------------------------------------------------- |
| `Name`   | `string`     | Unique identifier for the scenario (e.g., `"add-pagination"`) |
| `Repo`   | `RepoConfig` | The GitHub repository where the agent will work               |
| `Prompt` | `string`     | The task prompt sent to the agent                             |

### Step 3: Optionally Override Default Properties

You can override these virtual properties to customize behavior:

| Property       | Type                      | Default   | Description                                                 |
| -------------- | ------------------------- | --------- | ----------------------------------------------------------- |
| `Description`  | `string`                  | `""`      | Human-readable description of what the scenario tests       |
| `Tags`         | `string[]`                | `[]`      | Tags for filtering and categorization                       |
| `Timeout`      | `TimeSpan`                | 5 minutes | Maximum time allowed for the scenario                       |
| `AzsdkMcpPath` | `string?`                 | `null`    | Override path to azsdk-mcp executable                       |
| `TargetRepos`  | `IEnumerable<RepoConfig>` | `[]`      | Additional repos to clone as siblings (not yet implemented) |

### Step 4: Optionally Override SetupAsync

Override `SetupAsync(Workspace)` if your scenario needs to create or modify files before the agent runs:

```csharp
public override async Task SetupAsync(Workspace workspace)
{
    await workspace.WriteFileAsync("path/to/file.txt", "file contents");
}
```

## Example Scenario

Here's a complete example based on the `RenameClientPropertyScenario`:

```csharp
using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

public class MyScenario : BenchmarkScenario
{
    public override string Name => "my-scenario";

    public override string Description => "Description of what this tests";

    public override string[] Tags => ["typespec", "authoring"];

    public override RepoConfig Repo => new()
    {
        Owner = "Azure",
        Name = "azure-rest-api-specs",
        Ref = "main"
    };

    public override string Prompt => """
        Your prompt to the agent here.
        Be specific about what file to edit and what change to make.
        """;

    public override TimeSpan Timeout => TimeSpan.FromMinutes(3);
}
```

## RepoConfig Options

The `RepoConfig` class configures which GitHub repository the agent works in:

```csharp
public override RepoConfig Repo => new()
{
    Owner = "Azure",              // Required: GitHub org or user
    Name = "azure-rest-api-specs", // Required: Repository name
    Ref = "main",                 // Optional: Branch, tag, or commit (default: "main")
    ForkOwner = "myuser"          // Optional: Use a fork instead of the main repo
};
```

| Property    | Required | Description                                                               |
| ----------- | -------- | ------------------------------------------------------------------------- |
| `Owner`     | Yes      | GitHub organization or username (e.g., `"Azure"`)                         |
| `Name`      | Yes      | Repository name (e.g., `"azure-rest-api-specs"`)                          |
| `Ref`       | No       | Branch, tag, or commit SHA to checkout. Default: `"main"`                 |
| `ForkOwner` | No       | If set, clones from this owner instead. Useful for testing against forks. |

### Using a Fork

When `ForkOwner` is set, the repository is cloned from the fork while keeping the original `Owner` for reference:

```csharp
public override RepoConfig Repo => new()
{
    Owner = "Azure",                    // Original repo owner
    Name = "azure-rest-api-specs",
    ForkOwner = "chrisradek",           // Clone from this fork instead
    Ref = "my-feature-branch"
};
```

The `EffectiveOwner` property returns `ForkOwner` if set, otherwise `Owner`.

## Custom Setup

Override `SetupAsync(Workspace)` when your scenario needs to create or modify files before the agent runs.

### Workspace API

The `Workspace` class provides file operations:

```csharp
public override async Task SetupAsync(Workspace workspace)
{
    // Write a new file
    await workspace.WriteFileAsync(
        "specification/myservice/main.tsp",
        """
        import "@typespec/http";

        namespace MyService;

        @route("/users")
        op listUsers(): User[];
        """
    );

    // Read an existing file
    string content = await workspace.ReadFileAsync("some/existing/file.txt");

    // Modify and write back
    content = content.Replace("old-value", "new-value");
    await workspace.WriteFileAsync("some/existing/file.txt", content);
}
```

### When to Use Custom Setup

- Creating starter files for the agent to modify
- Setting up specific file states that don't exist in the repo
- Pre-populating configuration files
- Creating edge-case scenarios that require specific file content

Most scenarios don't need custom setup—they test against the repository as-is.

## Multi-Repo Scenarios (Not Yet Implemented)

> **Note**: Multi-repo support is designed but not yet implemented in the POC. The property exists for future use.

For scenarios that require multiple repositories, override `TargetRepos`:

```csharp
public override IEnumerable<RepoConfig> TargetRepos =>
[
    new RepoConfig
    {
        Owner = "Azure",
        Name = "azure-sdk-for-python",
        Ref = "main"
    }
];
```

When implemented, target repos will be cloned as flat siblings of the home repo, allowing the agent to navigate between them via `../repo-name`.

## Best Practices

### Use Shorter Timeouts for Simple Tasks

Don't use the default 5-minute timeout for tasks that should complete quickly:

```csharp
// Simple rename task - should complete in under a minute
public override TimeSpan Timeout => TimeSpan.FromMinutes(2);
```

### Add Descriptive Tags for Filtering

Tags help filter and categorize scenarios:

```csharp
public override string[] Tags => ["typespec", "authoring", "pagination", "poc"];
```

Common tag categories:

- **Technology**: `typespec`, `openapi`, `sdk`
- **Task type**: `authoring`, `review`, `generation`
- **Feature**: `pagination`, `error-handling`, `authentication`
- **Status**: `poc`, `stable`, `experimental`

### Test Scenarios Locally Before Committing

Run your scenario locally to verify it works before committing:

```bash
cd Azure.Sdk.Tools.Cli.Benchmarks

# List scenarios to verify yours is discovered
dotnet run -- list

# Run your specific scenario
dotnet run -- run my-scenario
```

## Running Your Scenario

Scenarios are **automatically discovered** via reflection. Any class that:

1. Inherits from `BenchmarkScenario`
2. Is not abstract
3. Has a parameterless constructor

...will be discovered and available to run.

### Using the CLI

```bash
# List all discovered scenarios
dotnet run -- list

# Run a specific scenario by name
dotnet run -- run my-scenario

# Run all scenarios
dotnet run -- run --all

# Run with options
dotnet run -- run my-scenario --cleanup never
```

### Using Tests (Optional)

For IDE integration or CI, you can also create NUnit tests:

```csharp
[TestFixture]
public class MyScenarioTests
{
    [Test]
    public async Task MyScenario_CompletesSuccessfully()
    {
        var scenario = new MyScenario();
        var options = new BenchmarkOptions
        {
            CleanupPolicy = CleanupPolicy.Always
        };

        using var runner = new BenchmarkRunner();
        var result = await runner.RunAsync(scenario, options);

        Assert.That(result.Passed, Is.True, result.Error);
    }
}
```

### Cleanup Policies

The `CleanupPolicy` option controls workspace cleanup:

| Policy      | Behavior                                  |
| ----------- | ----------------------------------------- |
| `Always`    | Always delete the workspace after the run |
| `OnSuccess` | Keep workspace on failure for debugging   |
| `Never`     | Always preserve the workspace             |

For local development, use `CleanupPolicy.OnSuccess` to inspect failures.
