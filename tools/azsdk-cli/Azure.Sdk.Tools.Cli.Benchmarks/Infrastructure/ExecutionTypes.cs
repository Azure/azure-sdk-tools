namespace Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

/// <summary>
/// Default values for benchmark execution.
/// </summary>
public static class BenchmarkDefaults
{
    /// <summary>
    /// The default model to use for benchmark execution.
    /// </summary>
    public const string DefaultModel = "claude-opus-4.5";
}

/// <summary>
/// Configuration for executing a benchmark scenario.
/// </summary>
public class ExecutionConfig
{
    /// <summary>Working directory for the agent (repo path).</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>The prompt to send to the agent.</summary>
    public required string Prompt { get; init; }

    /// <summary>Maximum time to wait for the agent to complete.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Path to azsdk MCP server executable (optional override).</summary>
    public string? AzsdkMcpPath { get; init; }

    /// <summary>The model to use for the agent session.</summary>
    public string Model { get; init; } = BenchmarkDefaults.DefaultModel;

    /// <summary>
    /// Optional callback for activity updates during execution.
    /// Called with activity description (e.g., "Calling tool: view").
    /// </summary>
    public Action<string>? OnActivity { get; init; }
}

/// <summary>
/// Result of executing a benchmark scenario.
/// </summary>
public class ExecutionResult
{
    /// <summary>Whether the execution completed successfully (no errors).</summary>
    public bool Completed { get; init; }

    /// <summary>Error message if execution failed.</summary>
    public string? Error { get; init; }

    /// <summary>Duration of the execution.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>The conversation messages from the session.</summary>
    public IReadOnlyList<object> Messages { get; init; } = [];

    /// <summary>Tool calls made during execution (for debugging).</summary>
    public IReadOnlyList<string> ToolCalls { get; init; } = [];
}
