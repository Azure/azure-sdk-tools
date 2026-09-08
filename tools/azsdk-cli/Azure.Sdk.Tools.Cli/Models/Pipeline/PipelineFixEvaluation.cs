// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

/// <summary>One merged pull request's progression through the automated pipeline-fix workflow.</summary>
public class PipelineFixEvaluation
{
    [JsonPropertyName("pr_number")]
    public required int PrNumber { get; set; }

    [JsonPropertyName("pr_title")]
    public string? PrTitle { get; set; }

    [JsonPropertyName("fix_workflow_run")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? FixWorkflowRun { get; set; }

    [JsonPropertyName("fix_branch_opened")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FixBranchOpened { get; set; }

    [JsonPropertyName("fix_pr_merged")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FixPullRequestMerged { get; set; }

    [JsonPropertyName("pipeline_outcome")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CopilotPipelineOutcome? PipelineOutcome { get; set; }

    [JsonPropertyName("verification")]
    public required CopilotFixVerification Verification { get; set; }
}

public class PipelineFixDateEvaluation
{
    [JsonPropertyName("date")]
    public required DateOnly Date { get; init; }

    [JsonPropertyName("evaluations")]
    public required List<PipelineFixEvaluation> Evaluations { get; init; }
}