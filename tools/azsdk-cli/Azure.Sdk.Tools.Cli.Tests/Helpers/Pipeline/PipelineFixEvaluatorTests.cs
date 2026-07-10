// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Helpers.Pipeline;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.Build.WebApi;
using Moq;
using Octokit;
using Octokit.Internal;
using GitHubPullRequest = Octokit.PullRequest;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Pipeline;

[TestFixture]
public class PipelineFixEvaluatorTests
{
    [Test]
    public async Task EvaluateAsync_ModelFailureRetainsUsageAndFetchesDiscoveredCommitDiffs()
    {
        var failedAt = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
        var succeededAt = failedAt.AddHours(2);
        var commitAt = failedAt.AddHours(1);
        var copilotCommit = CreateCommit(
            "copilot-sha",
            "Copilot",
            "copilot-swe-agent[bot]",
            commitAt);
        var humanCommit = CreateCommit(
            "human-sha",
            "maintainer",
            "Maintainer",
            commitAt.AddMinutes(10));

        var gitHubService = CreateGitHubService(copilotCommit, humanCommit);

        var devOpsService = new Mock<IDevOpsService>();
        devOpsService
            .Setup(s => s.GetPullRequestBuildsAsync(
                "Azure",
                "azure-sdk-for-test",
                42,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateBuild(100, BuildResult.Failed, failedAt),
                CreateBuild(101, BuildResult.Succeeded, succeededAt),
            ]);

        var tokenUsageHelper = new TokenUsageHelper(Mock.Of<IRawOutputHelper>());
        var agentRunner = new Mock<ICopilotAgentRunner>();
        agentRunner
            .Setup(r => r.RunAsync(
                It.IsAny<CopilotAgent<PipelineFixEvaluationJudgeVerdict>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => tokenUsageHelper.AddCumulative("test-model", 123, 45))
            .ThrowsAsync(new InvalidOperationException("The agent did not return a verdict."));

        var evaluator = new PipelineFixEvaluator(
            gitHubService.Object,
            devOpsService.Object,
            pipelineAnalysisTool: null!,
            agentRunner.Object,
            tokenUsageHelper,
            NullLogger<PipelineFixEvaluator>.Instance);

        var results = await evaluator.EvaluateAsync(
            "Azure",
            "azure-sdk-for-test",
            failedAt.AddDays(-1),
            succeededAt.AddDays(1),
            "test-model",
            dryRun: false,
            CancellationToken.None);

        var result = results.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.PRNumber, Is.EqualTo(42));
            Assert.That(result.FailedBuildId, Is.EqualTo(100));
            Assert.That(result.SucceededBuildId, Is.EqualTo(101));
            Assert.That(result.Outcome, Is.EqualTo(EvaluationOutcome.ModelError));
            Assert.That(result.ModelUsed, Is.EqualTo("test-model"));
            Assert.That(result.InputTokens, Is.EqualTo(123));
            Assert.That(result.OutputTokens, Is.EqualTo(45));
        });
        gitHubService.Verify(
            s => s.GetCommitFilesAsync(
                "Azure",
                "azure-sdk-for-test",
                "copilot-sha",
                It.IsAny<CancellationToken>()),
            Times.Once);
        gitHubService.Verify(
            s => s.GetCommitFilesAsync(
                "Azure",
                "azure-sdk-for-test",
                "human-sha",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task EvaluateAsync_DoesNotCombineBuildsFromDifferentProjects()
    {
        var failedAt = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
        var succeededAt = failedAt.AddHours(2);
        var gitHubService = CreateGitHubService(CreateCommit(
            "copilot-sha",
            "Copilot",
            "copilot-swe-agent[bot]",
            failedAt.AddHours(1)));
        var devOpsService = new Mock<IDevOpsService>();
        devOpsService
            .Setup(s => s.GetPullRequestBuildsAsync(
                "Azure",
                "azure-sdk-for-test",
                42,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateBuild(
                    100,
                    BuildResult.Failed,
                    failedAt,
                    projectId: new Guid("11111111-1111-1111-1111-111111111111")),
                CreateBuild(
                    101,
                    BuildResult.Succeeded,
                    succeededAt,
                    projectId: new Guid("22222222-2222-2222-2222-222222222222")),
            ]);

        var evaluator = new PipelineFixEvaluator(
            gitHubService.Object,
            devOpsService.Object,
            pipelineAnalysisTool: null!,
            Mock.Of<ICopilotAgentRunner>(),
            new TokenUsageHelper(Mock.Of<IRawOutputHelper>()),
            NullLogger<PipelineFixEvaluator>.Instance);

        var results = await evaluator.EvaluateAsync(
            "Azure",
            "azure-sdk-for-test",
            failedAt.AddDays(-1),
            succeededAt.AddDays(1),
            "test-model",
            dryRun: true,
            CancellationToken.None);

        Assert.That(results, Is.Empty);
    }

    private static Mock<IGitHubService> CreateGitHubService(params PullRequestCommit[] commits)
    {
        var pullRequest = Deserialize<GitHubPullRequest>(
            """
            {
              "number": 42,
              "title": "Fix the pipeline",
              "merged_at": "2026-07-01T13:00:00Z"
            }
            """);
        var requestComment = Deserialize<IssueComment>(
            """
            {
              "body": "@copilot fix the pipeline",
              "user": { "login": "maintainer" }
            }
            """);
        var gitHubService = new Mock<IGitHubService>();
        gitHubService
            .Setup(s => s.GetPullRequestByTimeFrameAsync(
                "Azure",
                "azure-sdk-for-test",
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([pullRequest]);
        gitHubService
            .Setup(s => s.GetIssueCommentsAsync(
                "Azure",
                "azure-sdk-for-test",
                42,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([requestComment]);
        gitHubService
            .Setup(s => s.GetPullRequestCommitsAsync(
                "Azure",
                "azure-sdk-for-test",
                42,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(commits);
        gitHubService
            .Setup(s => s.GetCommitFilesAsync(
                "Azure",
                "azure-sdk-for-test",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        gitHubService
            .Setup(s => s.GetPullRequestFilesAsync(
                "Azure",
                "azure-sdk-for-test",
                42,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return gitHubService;
    }

    private static PullRequestCommit CreateCommit(
        string sha,
        string login,
        string authorName,
        DateTimeOffset authoredAt)
    {
        var date = authoredAt.ToString("O");
        return Deserialize<PullRequestCommit>(
            $$"""
            {
              "sha": "{{sha}}",
              "author": { "login": "{{login}}" },
              "commit": {
                "author": {
                  "name": "{{authorName}}",
                  "date": "{{date}}"
                },
                "committer": {
                  "name": "{{authorName}}",
                  "date": "{{date}}"
                }
              }
            }
            """);
    }

    private static Build CreateBuild(
        int id,
        BuildResult result,
        DateTimeOffset queuedAt,
        Guid? projectId = null) =>
        new()
        {
            Id = id,
            Result = result,
            QueueTime = queuedAt.UtcDateTime,
            Definition = new DefinitionReference { Id = 7, Name = "test-pipeline" },
            Project = new()
            {
                Id = projectId ?? new Guid("11111111-1111-1111-1111-111111111111"),
                Name = "test-project",
            },
        };

    private static T Deserialize<T>(string json) =>
        new SimpleJsonSerializer().Deserialize<T>(json);
}
