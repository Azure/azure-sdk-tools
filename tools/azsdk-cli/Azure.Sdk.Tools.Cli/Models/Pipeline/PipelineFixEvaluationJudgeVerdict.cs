// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

/// <summary>
/// The structured verdict the model-judged tier asks the Copilot agent to produce. Used as the
/// agent's Exit-tool result schema, so the agent must return exactly these fields.
/// </summary>
public class PipelineFixEvaluationJudgeVerdict
{
    [Description("True if the Copilot commit's changes are present in the final merged pull request (not reverted or heavily rewritten). Whether the change survived, independent of whether it fixed the failure.")]
    public bool CopilotContributionSurvived { get; set; }

    [Description("True if the Copilot changes actually addressed the pipeline failure (rather than unrelated changes that happened to coincide with the pipeline going green). Independent of whether the change survived into the merged PR.")]
    public bool CopilotFixAddressedPipelineFailure { get; set; }

    [Description("True if the non-Copilot (human) changes in the window were irrelevant to fixing the pipeline failure, i.e. they did not themselves provide the fix.")]
    public bool NonCopilotChangesWereIrrelevantToFix { get; set; }

    [Description("A short, factual explanation (1-3 sentences) of how this verdict was reached.")]
    public string Reasoning { get; set; } = string.Empty;
}
