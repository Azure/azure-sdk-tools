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
    /// <summary>The trigger could not be identified.</summary>
    Unknown = 0,

    /// <summary>
    /// The pipeline-analysis auto-fix workflow produced the commit on a copilot-pipeline-fix/pr-N branch.
    /// </summary>
    GitHubActionsWorkflow,

    /// <summary>Somebody directed Copilot at the pull request with an @copilot mention.</summary>
    CopilotMention,
}

/// <summary>
/// Whether the Copilot fix changed the pipeline outcome. The collection phase only emits a candidate once
/// a check that ran on both the commit and its parent changed state, so there is no third state: every
/// candidate that reaches telemetry either fixed a check or broke one.
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
/// Whether the Copilot fix survived into the merged pull request, independent of whether it
/// fixed the pipeline.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CopilotFixVerification
{
    /// <summary>
    /// The judge never ran: the Copilot SDK call failed (rate limit, 5xx, dropped connection). This is an
    /// infrastructure failure on our side, not a verdict about the commit.
    /// </summary>
    Undetermined = 0,

    /// <summary>No human commits landed after the fix, so it survived unmodified into the merge.</summary>
    CopilotVerifiedFix,

    /// <summary>Mixed history, but the model judged Copilot's change was not overridden.</summary>
    CopilotJudgeVerifiedFix,

    /// <summary>Mixed history, and the model judged Copilot's change was overridden.</summary>
    CopilotJudgeVerifiedFailure,

    /// <summary>The pipeline never recovered across the commit, so there is no fix whose survival can be judged.</summary>
    NotApplicable,
}
