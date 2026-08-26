// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Pipeline;

namespace Azure.Sdk.Tools.Cli.Tests.Models;

/// <summary>
/// Locks the JSON wire contract downstream consumers read by name: every key is lower snake_case, and
/// the two outcome axes serialize as their exact string names. If a global JSON naming policy is ever
/// added to <see cref="OutputHelper"/>, or a property is renamed, these assertions fail loudly instead
/// of silently nulling a consumer's columns. Serialization goes through the real
/// <see cref="OutputHelper"/> so a change to its options is also caught.
/// </summary>
[TestFixture]
public class PipelineFixEvaluatorResponseSerializationTests
{
    private static JsonElement SerializeThroughOutputHelper()
    {
        var response = new PipelineFixEvaluatorResponse
        {
            Owner = "Azure",
            Repo = "azure-sdk-for-net",
            Since = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            Until = new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero),
            Results =
            [
                new CopilotPipelineFixResult
                {
                    PrNumber = 123,
                    PrTitle = "Fix flaky pipeline",
                    FixBranch = "pipeline-fix/pr-123-abc123/run-456",
                    Trigger = CopilotFixTrigger.GitHubActionsWorkflow,
                    ChecksFixed = ["static-analysis"],
                    ChecksBroken = [],
                    CopilotCommitShas = ["abc123def456"],
                    PipelineOutcome = CopilotPipelineOutcome.CopilotPipelineFixSuccess,
                    Verification = CopilotFixVerification.CopilotVerifiedFix,
                    AnalysisCommentPresent = true,
                },
                new CopilotPipelineFixResult
                {
                    PrNumber = 124,
                    PrTitle = "Fix flaky pipeline, with human follow-up",
                    Trigger = CopilotFixTrigger.CopilotMention,
                    ChecksFixed = ["Test Azure SDK Tools"],
                    ChecksBroken = [],
                    CopilotCommitShas = ["def456abc123"],
                    PipelineOutcome = CopilotPipelineOutcome.CopilotPipelineFixSuccess,
                    Verification = CopilotFixVerification.CopilotVerifiedFix,
                    AnalysisCommentPresent = false,
                },
            ],
        };

        // MCP mode uses the serializer options the tool ships (no naming policy), just compact.
        var json = new OutputHelper(OutputHelper.OutputModes.Mcp).Format(response);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Test]
    public void Response_PinsWrapperAndBaseKeys()
    {
        var root = SerializeThroughOutputHelper();
        Assert.Multiple(() =>
        {
            foreach (var key in new[] { "owner", "repo", "since", "until", "results" })
            {
                Assert.That(root.TryGetProperty(key, out _), Is.True, $"wrapper key '{key}' missing");
            }
            // Base CommandResponse contract.
            Assert.That(root.GetProperty("operation_status").GetString(), Is.EqualTo("Succeeded"));
        });
    }

    [Test]
    public void Response_PinsResultKeysToSnakeCase()
    {
        var root = SerializeThroughOutputHelper();
        var result = root.GetProperty("results")[0];
        Assert.Multiple(() =>
        {
            foreach (var key in new[]
            {
                "pr_number", "pr_title", "fix_branch", "trigger", "checks_fixed", "checks_broken",
                "copilot_commit_shas", "pipeline_outcome", "verification", "analysis_comment_present",
            })
            {
                Assert.That(result.TryGetProperty(key, out _), Is.True, $"result key '{key}' missing");
            }
            // A global camelCase policy would rename these and silently break every consumer.
            Assert.That(result.TryGetProperty("prNumber", out _), Is.False);
            Assert.That(result.TryGetProperty("PRNumber", out _), Is.False);
            Assert.That(result.GetProperty("pipeline_outcome").ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(result.GetProperty("pipeline_outcome").GetString(), Is.EqualTo("CopilotPipelineFixSuccess"));
            Assert.That(result.GetProperty("verification").ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(result.GetProperty("verification").GetString(), Is.EqualTo("CopilotVerifiedFix"));
            Assert.That(result.GetProperty("trigger").ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(result.GetProperty("trigger").GetString(), Is.EqualTo("GitHubActionsWorkflow"));
        });
    }

    // A pull request can be fixed more than once, so pr_number is not a unique key. The workflow branch
    // identifies each attempt; an @copilot mention has no separate branch and must omit it.
    [Test]
    public void Response_IdentifiesAttemptsSharingAPullRequest()
    {
        var root = SerializeThroughOutputHelper();
        var workflowResult = root.GetProperty("results")[0];
        var mentionResult = root.GetProperty("results")[1];
        Assert.Multiple(() =>
        {
            Assert.That(workflowResult.GetProperty("trigger").GetString(), Is.EqualTo("GitHubActionsWorkflow"));
            Assert.That(workflowResult.GetProperty("fix_branch").GetString(), Is.EqualTo("pipeline-fix/pr-123-abc123/run-456"));
            Assert.That(mentionResult.GetProperty("trigger").GetString(), Is.EqualTo("CopilotMention"));
            Assert.That(mentionResult.TryGetProperty("fix_branch", out _), Is.False);
        });
    }

    [Test]
    public void UnusedWorkflowBranch_OmitsPipelineOutcome()
    {
        var result = JsonSerializer.SerializeToElement(new CopilotPipelineFixResult
        {
            PrNumber = 125,
            FixBranch = "pipeline-fix/pr-125-abc123/run-456",
            Trigger = CopilotFixTrigger.GitHubActionsWorkflow,
            CopilotCommitShas = ["abc123def456"],
            Verification = CopilotFixVerification.CopilotFixNotMerged,
            AnalysisCommentPresent = true,
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.TryGetProperty("pipeline_outcome", out _), Is.False);
            Assert.That(result.GetProperty("verification").GetString(), Is.EqualTo("CopilotFixNotMerged"));
        });
    }

    [Test]
    public void Response_EveryKeyIsLowerSnakeCase()
    {
        var root = SerializeThroughOutputHelper();
        Assert.Multiple(() =>
        {
            foreach (var property in root.EnumerateObject())
            {
                AssertLowerSnakeCase(property.Name);
            }
            foreach (var result in root.GetProperty("results").EnumerateArray())
            {
                foreach (var property in result.EnumerateObject())
                {
                    AssertLowerSnakeCase(property.Name);
                }
            }
        });

        static void AssertLowerSnakeCase(string name) =>
            Assert.That(
                name,
                Does.Match("^[a-z0-9]+(_[a-z0-9]+)*$"),
                $"wire key '{name}' is not lower snake_case");
    }

    [Test]
    public void Response_WithNoResults_ReportsNothingToEvaluate()
    {
        var response = new PipelineFixEvaluatorResponse
        {
            Owner = "Azure",
            Repo = "azure-sdk-for-net",
            Results = [],
        };

        Assert.That(response.ToString(), Does.Contain("No Copilot pipeline fixes"));
    }

    // Consumers filter and group on these exact outcome strings. Renaming an enum member (or dropping
    // [JsonStringEnumConverter] so it serializes as an int) silently breaks those queries, so pin every
    // wire string here.
    [TestCase(CopilotPipelineOutcome.CopilotPipelineFixSuccess, "CopilotPipelineFixSuccess")]
    [TestCase(CopilotPipelineOutcome.CopilotPipelineFixFailure, "CopilotPipelineFixFailure")]
    public void CopilotPipelineOutcome_SerializesToExactWireString(CopilotPipelineOutcome outcome, string expected)
    {
        var json = JsonSerializer.Serialize(outcome);
        Assert.That(json, Is.EqualTo($"\"{expected}\""));
    }

    [TestCase(CopilotFixVerification.CopilotVerifiedFix, "CopilotVerifiedFix")]
    [TestCase(CopilotFixVerification.CopilotFixOverridden, "CopilotFixOverridden")]
    [TestCase(CopilotFixVerification.NotApplicable, "NotApplicable")]
    [TestCase(CopilotFixVerification.CopilotFixNotMerged, "CopilotFixNotMerged")]
    public void CopilotFixVerification_SerializesToExactWireString(CopilotFixVerification verification, string expected)
    {
        var json = JsonSerializer.Serialize(verification);
        Assert.That(json, Is.EqualTo($"\"{expected}\""));
    }
}
