// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Interaction;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

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

    /// <summary>List of questions asked by the model and the corresponding answers provided during execution.</summary>
    public IReadOnlyList<QuestionAndAnswer> InputQuestionAndAnswers { get; init; } = [];
}
