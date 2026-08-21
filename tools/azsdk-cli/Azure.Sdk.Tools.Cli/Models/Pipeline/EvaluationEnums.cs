// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;


/// <summary>
/// How Copilot came to make the commit. The two paths deliver a fix differently - the workflow opens a
/// separate pull request, a mention pushes onto the branch in place - so the trigger is what tells a
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
/// Whether the Copilot fix was carried through to the pull request that merged to main, independent of
/// whether it fixed the pipeline. For an @copilot mention this is survival of the change through any later
/// human commits; for the auto-fix workflow it is adoption.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CopilotFixVerification
{
    /// <summary>
    /// The judge never ran: the Copilot SDK call failed (rate limit, 5xx, dropped connection). This is an
    /// infrastructure failure on our side, not a verdict about the commit.
    /// </summary>
    Undetermined = 0,

    /// <summary>
    /// The fix reached the merge unmodified: either no human commits landed after an @copilot fix,
    /// or the auto-fix workflow's fix pull request was merged into the original.
    /// </summary>
    CopilotVerifiedFix,

    /// <summary>Mixed history, but the model judged Copilot's change was not overridden.</summary>
    CopilotJudgeVerifiedFix,

    /// <summary>Mixed history, and the model judged Copilot's change was overridden.</summary>
    CopilotJudgeVerifiedFailure,

    /// <summary>The pipeline never recovered across the commit, so there is no fix whose survival can be judged.</summary>
    NotApplicable,

    /// <summary>The auto-fix workflow's fix pull request fixed the pipeline in isolation but was not merged
    /// into the original pull request, so the fix was not adopted.</summary>
    CopilotFixNotMerged,
}
