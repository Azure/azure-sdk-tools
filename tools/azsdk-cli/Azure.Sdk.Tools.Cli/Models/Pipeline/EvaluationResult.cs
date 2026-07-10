// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

/// <summary>
/// The verdict for one evaluation candidate: whether the Copilot fix worked and survived into the
/// merged PR, how that was decided, and trendable metrics.
/// </summary>
public class EvaluationResult
{
    /// <summary>The pull request number.</summary>
    public int PRNumber { get; set; }

    /// <summary>The pull request title.</summary>
    public string? PRTitle { get; set; }

    /// <summary>The build ID of the failing run that opened this evaluation window.</summary>
    public int FailedBuildId { get; set; }

    /// <summary>The build ID of the succeeding run that closed this evaluation window.</summary>
    public int SucceededBuildId { get; set; }

    /// <summary>The final outcome.</summary>
    public EvaluationOutcome Outcome { get; set; }

    /// <summary>Human-readable explanation of how the verdict was reached.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>The model used by the model-judged tier, if it ran.</summary>
    public string? ModelUsed { get; set; }

    /// <summary>Input tokens consumed by the model tier (0 if it did not run).</summary>
    public double InputTokens { get; set; }

    /// <summary>Output tokens consumed by the model tier (0 if it did not run).</summary>
    public double OutputTokens { get; set; }
}
