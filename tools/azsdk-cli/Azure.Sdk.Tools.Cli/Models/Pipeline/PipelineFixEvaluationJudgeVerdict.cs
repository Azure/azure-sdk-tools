// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

/// <summary>
/// The structured verdict the model-judged tier asks the Copilot agent to produce. Used as the
/// agent's Exit-tool result schema, so the agent must return exactly these fields. It is also
/// emitted as part of the telemetry payload, so the wire names are pinned like the rest of it.
/// </summary>
public class PipelineFixEvaluationJudgeVerdict
{
    [JsonPropertyName("copilot_contribution_survived")]
    [Description("True if the Copilot commit's changes are present in the final merged pull request (not reverted or heavily rewritten). Whether the change survived, independent of whether it fixed the failure.")]
    public required bool CopilotContributionSurvived { get; set; }

    [JsonPropertyName("copilot_fix_addressed_pipeline_failure")]
    [Description("True if the Copilot changes actually addressed the pipeline failure (rather than unrelated changes that happened to coincide with the pipeline going green). Independent of whether the change survived into the merged PR.")]
    public required bool CopilotFixAddressedPipelineFailure { get; set; }

    [JsonPropertyName("human_changes_were_irrelevant_to_fix")]
    [Description("True if the human changes in the window were irrelevant to fixing the pipeline failure, i.e. they did not themselves provide the fix.")]
    public required bool HumanChangesWereIrrelevantToFix { get; set; }

    [JsonPropertyName("reasoning")]
    [Description("A short, factual explanation (1-3 sentences) of how this verdict was reached.")]
    public required string Reasoning { get; set; }
}
