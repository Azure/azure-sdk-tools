// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;


/// <summary>
/// How Copilot came to make the commit. The two paths deliver a fix differently - the workflow publishes a
/// separate branch, a mention pushes onto the pull request branch in place - so the trigger is what tells a
/// reader which delivery path an outcome is a verdict on.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CopilotFixTrigger
{
    /// The pipeline-analysis auto-fix workflow produced the commit on a pipeline-fix/pr-N branch.
    GitHubActionsWorkflow,

    /// Somebody directed Copilot at the pull request with an @copilot mention.
    CopilotMention,
}

/// <summary>
/// Whether the Copilot fix changed the pipeline outcome.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CopilotPipelineOutcome
{
    /// <summary>A check failing on the parent passed on the commit, and none regressed.</summary>
    CopilotPipelineFixSuccess,

    /// <summary>A check passing on the parent failed on the commit, whatever else was fixed alongside it.</summary>
    CopilotPipelineFixFailure,
}

/// <summary>
/// Deterministic verification of a Copilot fix. Mention fixes are verified by their check transition;
/// workflow fixes are verified by adoption into the original pull request.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CopilotFixVerification
{
    /// <summary>
    /// The mention fixed a failing check without later human overlap, or the workflow branch commit entered the original pull request.
    /// </summary>
    CopilotVerifiedFix,

    /// <summary>A later human edit touched the Copilot fix, or a fixed check was explicitly failing at merge.</summary>
    CopilotFixOverridden,

    /// <summary>The pipeline never recovered across the commit, so there is no fix whose survival can be judged.</summary>
    NotApplicable,

    /// <summary>The auto-fix workflow published a branch that was never used in the original pull request.</summary>
    CopilotFixNotMerged,
}
