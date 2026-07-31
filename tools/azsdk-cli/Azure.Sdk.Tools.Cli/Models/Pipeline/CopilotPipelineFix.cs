// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Octokit;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

/// <summary>
/// A Copilot pipeline-fix candidate collected in phase one: the merged pull request Copilot fixed, the
/// commits it made (the "after" side), the parent commits they were applied on top of (the "before" side),
/// and the CI check state on each side. It is the input to phase-two grading and is not itself telemetry.
/// </summary>
public class CopilotPipelineFix
{
    /// <summary>The pull request that merged to main. A single number is kept for both triggers: an
    /// @copilot mention pushes onto the same pull request, and the auto-fix workflow's branch is the one
    /// that merges, so there is no separate source-vs-fixed number to track.</summary>
    public required int PrNumber { get; init; }

    public string? PrTitle { get; init; }

    /// <summary>The repository owner the candidate was collected from, carried so phase two can fetch diffs.</summary>
    public required string Owner { get; init; }

    /// <summary>The repository name the candidate was collected from, carried so phase two can fetch diffs.</summary>
    public required string Repo { get; init; }

    public required CopilotFixTrigger Trigger { get; init; }

    /// <summary>The Copilot commit SHAs on the merged pull request - the "after" side of the comparison.</summary>
    public required IReadOnlyList<string> CopilotCommitShas { get; init; }

    /// <summary>The non-Copilot parents of the Copilot commits - the "before" side of the comparison.</summary>
    public required IReadOnlyList<string> BeforeShas { get; init; }

    /// <summary>CI check name to whether it failed, taken on the before side.</summary>
    public required IReadOnlyDictionary<string, bool> ChecksBefore { get; init; }

    /// <summary>CI check name to whether it failed, taken on the after side.</summary>
    public required IReadOnlyDictionary<string, bool> ChecksAfter { get; init; }

    /// <summary>The full commit list of the merged pull request, used to trace human commits after the fix.</summary>
    public required IReadOnlyList<PullRequestCommit> Commits { get; init; }
}
