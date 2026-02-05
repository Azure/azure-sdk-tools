namespace Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

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
