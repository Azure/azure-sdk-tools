// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Pipeline;

namespace Azure.Sdk.Tools.Cli.Tests.Models;

[TestFixture]
public class PipelineFixEvaluatorResponseSerializationTests
{
    [Test]
    public void Response_SerializesOwnerRepoAndDatedEvaluations()
    {
        var response = new PipelineFixEvaluatorResponse
        {
            Owner = "Azure",
            Repo = "azure-sdk-for-net",
            Dates =
            [
                new PipelineFixDateEvaluation
                {
                    Date = new DateOnly(2026, 8, 31),
                    Evaluations =
                    [
                        new PipelineFixEvaluation
                        {
                            PrNumber = 123,
                            PrTitle = "Fix pipeline",
                            FixWorkflowRun = 30861672656,
                            FixBranchOpened = "pipeline-fix/pr-123-abc123/run-30861672656",
                            FixPullRequestMerged = "abc123",
                            PipelineOutcome = CopilotPipelineOutcome.CopilotPipelineFixSuccess,
                            Verification = CopilotFixVerification.CopilotVerifiedFix,
                        },
                    ],
                },
                new PipelineFixDateEvaluation
                {
                    Date = new DateOnly(2026, 8, 30),
                    Evaluations = [],
                },
            ],
        };

        var json = new OutputHelper(OutputHelper.OutputModes.Mcp).Format(response);
        var root = JsonDocument.Parse(json).RootElement;
        var dates = root.GetProperty("dates");
        var row = dates[0].GetProperty("evaluations")[0];

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("owner").GetString(), Is.EqualTo("Azure"));
            Assert.That(root.GetProperty("repo").GetString(), Is.EqualTo("azure-sdk-for-net"));
            Assert.That(root.GetProperty("operation_status").GetString(), Is.EqualTo("Succeeded"));
            Assert.That(dates.GetArrayLength(), Is.EqualTo(2));
            Assert.That(dates[0].GetProperty("date").GetString(), Is.EqualTo("2026-08-31"));
            Assert.That(row.GetProperty("pr_number").GetInt32(), Is.EqualTo(123));
            Assert.That(row.GetProperty("pr_title").GetString(), Is.EqualTo("Fix pipeline"));
            Assert.That(row.GetProperty("fix_workflow_run").GetInt64(), Is.EqualTo(30861672656));
            Assert.That(row.GetProperty("fix_branch_opened").GetString(), Is.EqualTo("pipeline-fix/pr-123-abc123/run-30861672656"));
            Assert.That(row.GetProperty("fix_pr_merged").GetString(), Is.EqualTo("abc123"));
            Assert.That(row.GetProperty("pipeline_outcome").GetString(), Is.EqualTo("CopilotPipelineFixSuccess"));
            Assert.That(row.GetProperty("verification").GetString(), Is.EqualTo("CopilotVerifiedFix"));
        });
    }

    [Test]
    public void Response_OmitsNullableStagesThatWereNotReached()
    {
        var row = JsonSerializer.SerializeToElement(new PipelineFixEvaluation
        {
            PrNumber = 9,
            PrTitle = "No workflow",
            Verification = CopilotFixVerification.NotApplicable,
        });

        Assert.Multiple(() =>
        {
            Assert.That(row.TryGetProperty("fix_workflow_run", out _), Is.False);
            Assert.That(row.TryGetProperty("fix_branch_opened", out _), Is.False);
            Assert.That(row.TryGetProperty("fix_pr_merged", out _), Is.False);
            Assert.That(row.TryGetProperty("pipeline_outcome", out _), Is.False);
        });
    }

    [TestCase(CopilotPipelineOutcome.CopilotPipelineFixSuccess, "CopilotPipelineFixSuccess")]
    [TestCase(CopilotPipelineOutcome.CopilotPipelineFixFailure, "CopilotPipelineFixFailure")]
    public void PipelineOutcome_SerializesAsString(CopilotPipelineOutcome value, string expected) =>
        Assert.That(JsonSerializer.Serialize(value), Is.EqualTo($"\"{expected}\""));

    [TestCase(CopilotFixVerification.CopilotVerifiedFix, "CopilotVerifiedFix")]
    [TestCase(CopilotFixVerification.CopilotFixOverridden, "CopilotFixOverridden")]
    [TestCase(CopilotFixVerification.NotApplicable, "NotApplicable")]
    [TestCase(CopilotFixVerification.CopilotFixNotMerged, "CopilotFixNotMerged")]
    public void Verification_SerializesAsString(CopilotFixVerification value, string expected) =>
        Assert.That(JsonSerializer.Serialize(value), Is.EqualTo($"\"{expected}\""));
}
