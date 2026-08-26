// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models.Pipeline;

namespace Azure.Sdk.Tools.Cli.Tests.Models;

/// <summary>
/// Locks the vocabulary of the evaluator's telemetry against the pipeline-analysis dashboard, which renders
/// it from a different repository and so cannot be caught by a compiler.
///
/// The sibling serialization tests pin each enum member to its wire string, which stops a member being
/// renamed. They do not stop one being *added*, and adding is the failure that actually happened:
/// <see cref="CopilotFixVerification.CopilotFixNotMerged"/> shipped without the dashboard knowing it, so a
/// fix the workflow proved but nobody adopted was silently charted incorrectly.
///
/// The lists below mirror the dashboard's own, in
/// tools/pipeline-analysis-dashboard/lib/metric-sources.js. Adding a member to one of these enums fails
/// here until that file learns to render it.
/// </summary>
[TestFixture]
public class CopilotPipelineFixDashboardContractTests
{
    // metric-sources.js: PIPELINE_OUTCOMES badges, and the COMBINED_CATEGORIES bucket each one feeds.
    private static readonly string[] DashboardPipelineOutcomes =
    [
        "CopilotPipelineFixSuccess",
        "CopilotPipelineFixFailure",
    ];

    // metric-sources.js: FIX_VERIFICATIONS badges.
    private static readonly string[] DashboardVerifications =
    [
        "CopilotVerifiedFix",
        "CopilotFixOverridden",
        "NotApplicable",
        "CopilotFixNotMerged",
    ];

    // metric-sources.js: FIX_TRIGGERS badges, which label the delivery path in the detail table.
    private static readonly string[] DashboardTriggers =
    [
        "GitHubActionsWorkflow",
        "CopilotMention",
    ];

    private const string Guidance =
        "The pipeline-analysis dashboard renders this enum by value. Add the new member to the matching "
        + "list in tools/pipeline-analysis-dashboard/lib/metric-sources.js (and give it a chart category "
        + "where the outcome axes cross), then add it here.";

    [Test]
    public void CopilotPipelineOutcome_MembersAreAllRenderedByTheDashboard() =>
        Assert.That(Enum.GetNames<CopilotPipelineOutcome>(), Is.EquivalentTo(DashboardPipelineOutcomes), Guidance);

    [Test]
    public void CopilotFixVerification_MembersAreAllRenderedByTheDashboard() =>
        Assert.That(Enum.GetNames<CopilotFixVerification>(), Is.EquivalentTo(DashboardVerifications), Guidance);

    [Test]
    public void CopilotFixTrigger_MembersAreAllRenderedByTheDashboard() =>
        Assert.That(Enum.GetNames<CopilotFixTrigger>(), Is.EquivalentTo(DashboardTriggers), Guidance);

    // The dashboard tells workflow attempts apart by fix branch. A mention has no separate branch, but still
    // carries its trigger so the delivery path remains explicit.
    [Test]
    public void MentionResult_OmitsFixBranchButStillCarriesTrigger()
    {
        var json = JsonSerializer.Serialize(new CopilotPipelineFixResult
        {
            PrNumber = 9,
            Trigger = CopilotFixTrigger.CopilotMention,
            PipelineOutcome = CopilotPipelineOutcome.CopilotPipelineFixSuccess,
            Verification = CopilotFixVerification.CopilotVerifiedFix,
            AnalysisCommentPresent = false,
        });
        var row = JsonDocument.Parse(json).RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(row.TryGetProperty("fix_branch", out _), Is.False);
            Assert.That(row.GetProperty("trigger").GetString(), Is.EqualTo("CopilotMention"));
        });
    }
}
