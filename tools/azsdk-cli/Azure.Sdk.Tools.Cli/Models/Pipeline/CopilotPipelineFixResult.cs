// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

/// <summary>
/// One telemetry row: a Copilot pipeline-fix attempt on a merged pull request, graded on whether the
/// pipeline recovered across the fix and whether the change survived into the merge. A row is only built
/// once a check that ran on both sides changed state, so every row is a real success or failure rather
/// than an absence of evidence. Wire names are pinned because the row is emitted as telemetry.
/// </summary>
public class CopilotPipelineFixResult
{
    [JsonPropertyName("pr_number")]
    public required int PrNumber { get; set; }

    [JsonPropertyName("pr_title")]
    public string? PrTitle { get; set; }

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

    /// <summary>The model judge's supporting signals and reasoning, kept as an audit trail.</summary>
    [JsonPropertyName("judge_verdict")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PipelineFixEvaluationJudgeVerdict? JudgeVerdict { get; set; }
}
