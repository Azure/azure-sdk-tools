// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

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
/// Deterministic verification of an automated workflow fix.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CopilotFixVerification
{
    /// <summary>
    /// The workflow branch commit entered the original pull request and its fixed checks remained green.
    /// </summary>
    CopilotVerifiedFix,

    /// <summary>A later human commit rewrote lines changed by the adopted workflow fix.</summary>
    CopilotFixOverridden,

    /// <summary>The pipeline never recovered across the commit, so there is no fix whose survival can be judged.</summary>
    NotApplicable,

    /// <summary>The auto-fix workflow published a branch that was never used in the original pull request.</summary>
    CopilotFixNotMerged,
}
