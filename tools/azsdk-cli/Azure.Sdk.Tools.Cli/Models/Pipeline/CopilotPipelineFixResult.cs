// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

/// <summary>
/// One telemetry row: a single Copilot pipeline-fix attempt, graded on whether the pipeline recovered
/// across the fix and whether the fix was carried into the pull request that merged to main. A row is only
/// built once a check that ran on both sides changed state, so every row is a real success or failure
/// rather than an absence of evidence. Wire names are pinned because the row is emitted as telemetry.
///
/// The attempt is the unit, not the pull request, so PrNumber is NOT unique: one pull request
/// can be fixed by an @copilot mention and by the auto-fix workflow, and the workflow can open several fix
/// pull requests against it in succession.
/// </summary>
public class CopilotPipelineFixResult
{
    [JsonPropertyName("pr_number")]
    public required int PrNumber { get; set; }

    [JsonPropertyName("pr_title")]
    public string? PrTitle { get; set; }

    /// <summary>
    /// The separate pull request the auto-fix workflow opened to carry the fix. Null for an @copilot
    /// mention. Differentiates two rows with the same original pull request.
    /// </summary>
    [JsonPropertyName("fix_pr_number")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FixPrNumber { get; set; }

    [JsonPropertyName("trigger")]
    public required CopilotFixTrigger Trigger { get; set; }

    [JsonPropertyName("copilot_commit_shas")]
    public List<string> CopilotCommitShas { get; set; } = [];

    /// <summary>The checks that were failing on the before side and passed on the after side.</summary>
    [JsonPropertyName("checks_fixed")]
    public List<string> ChecksFixed { get; set; } = [];

    /// <summary>The checks that were passing on the before side and failed on the after side.</summary>
    [JsonPropertyName("checks_broken")]
    public List<string> ChecksBroken { get; set; } = [];

    [JsonPropertyName("pipeline_outcome")]
    public required CopilotPipelineOutcome PipelineOutcome { get; set; }

    [JsonPropertyName("verification")]
    public required CopilotFixVerification Verification { get; set; }

    [JsonPropertyName("analysis_comment_present")]
    public required bool AnalysisCommentPresent { get; set; }

    /// <summary>The model judge's supporting signals and reasoning, kept as an audit trail.</summary>
    [JsonPropertyName("judge_verdict")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PipelineFixEvaluationJudgeVerdict? JudgeVerdict { get; set; }
}
