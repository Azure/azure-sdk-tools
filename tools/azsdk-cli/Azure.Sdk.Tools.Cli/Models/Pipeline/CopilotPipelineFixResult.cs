// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

/// <summary>
/// One telemetry row: a single Copilot pipeline-fix attempt. Adopted fixes are graded on whether the
/// pipeline recovered; an unused workflow branch is retained without a pipeline outcome. Wire names are
/// pinned because the row is emitted as telemetry.
///
/// The attempt is the unit, not the pull request, so PrNumber is NOT unique: one pull request
/// can be fixed by an @copilot mention and by the auto-fix workflow, and the workflow can publish several
/// fix branches against it in succession.
/// </summary>
public class CopilotPipelineFixResult
{
    [JsonPropertyName("pr_number")]
    public required int PrNumber { get; set; }

    [JsonPropertyName("pr_title")]
    public string? PrTitle { get; set; }

    /// <summary>The branch published by the auto-fix workflow. Null for an @copilot mention.</summary>
    [JsonPropertyName("fix_branch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FixBranch { get; set; }

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

    /// <summary>Null when a workflow branch was never used in the pull request.</summary>
    [JsonPropertyName("pipeline_outcome")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CopilotPipelineOutcome? PipelineOutcome { get; set; }

    [JsonPropertyName("verification")]
    public required CopilotFixVerification Verification { get; set; }

    [JsonPropertyName("analysis_comment_present")]
    public required bool AnalysisCommentPresent { get; set; }
}
