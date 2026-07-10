// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

/// <summary>
/// The outcome of evaluating one Copilot pipeline-fix (a FAILURE -> SUCCESS window containing Copilot
/// commits): whether Copilot's fix worked and survived into the merged PR, and how that was decided.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EvaluationOutcome
{
    /// <summary>Deterministic success: Copilot commits were the only commits in the FAILURE -> SUCCESS
    /// window and no human commit landed after the fix, so the fix is attributable to Copilot and
    /// survived unmodified into the merged PR.</summary>
    PipelineFixSuccess,

    /// <summary>Model-judged success: a language model decided the Copilot fix survived into the merged PR.</summary>
    ModelJudgedSuccess,

    /// <summary>Model-judged failure: a language model decided the Copilot fix did not survive (reverted or
    /// heavily rewritten) into the merged PR.</summary>
    ModelJudgedFailure,

    /// <summary>The model tier was required (attribution was ambiguous) but skipped (e.g. dry-run mode).</summary>
    Skipped,

    /// <summary>The model tier was required but failed to produce a verdict (e.g. Copilot CLI error).</summary>
    ModelError,
}
