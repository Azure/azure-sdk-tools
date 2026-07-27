// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Helpers.Pipeline;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Pipeline;

/// <summary>
/// Seam: <see cref="GitHubWorkflowAnalysisHelper"/> turns a resolved <see cref="BuildGitHubSource"/> into
/// workflow-run analyses and failing PR checks, given <see cref="IGitHubService"/> and
/// <see cref="ILogAnalysisHelper"/>.
/// </summary>
[TestFixture]
public class GitHubWorkflowAnalysisHelperTests
{
    // Shapes taken from a real azure-sdk-for-net pull request run.
    private const string Owner = "Azure";
    private const string Repo = "azure-sdk-for-net";
    private const string HeadSha = "0f1a2b3c4d5e6f708192a3b4c5d6e7f809a1b2c3";
    private const int PrNumber = 44941;

    private static readonly BuildGitHubSource PrSource = new(Owner, Repo, HeadSha, PrNumber);
    private static readonly BuildGitHubSource BranchSource = new(Owner, Repo, HeadSha, null);

    private Mock<IGitHubService> gitHubService;
    private Mock<ILogAnalysisHelper> logAnalysisHelper;
    private GitHubWorkflowAnalysisHelper helper;

    [SetUp]
    public void SetUp()
    {
        gitHubService = new Mock<IGitHubService>(MockBehavior.Loose);
        logAnalysisHelper = new Mock<ILogAnalysisHelper>(MockBehavior.Loose);
        helper = new GitHubWorkflowAnalysisHelper(
            gitHubService.Object,
            logAnalysisHelper.Object,
            new TestLogger<GitHubWorkflowAnalysisHelper>());
    }

    #region AnalyzeWorkflowsAsync

    [Test]
    public async Task AnalyzeWorkflowsAsync_Always_RequestsFailedRunsForTheSourceCommit()
    {
        gitHubService
            .Setup(g => g.GetFailedWorkflowRunsForCommitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WorkflowRun>());

        await helper.AnalyzeWorkflowsAsync(PrSource, CancellationToken.None);

        gitHubService.Verify(
            g => g.GetFailedWorkflowRunsForCommitAsync(Owner, Repo, HeadSha, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task AnalyzeWorkflowsAsync_FailedRun_ReportsRunIdentity()
    {
        GivenWorkflowRuns(WorkflowRunFor(
            id: 12345678,
            name: "analyze",
            status: WorkflowRunStatus.Completed,
            conclusion: WorkflowRunConclusion.Failure));

        var analysis = (await helper.AnalyzeWorkflowsAsync(PrSource, CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(analysis.Name, Is.EqualTo("analyze"));
            Assert.That(analysis.Status, Is.EqualTo("completed"));
            Assert.That(analysis.Conclusion, Is.EqualTo("failure"));
            Assert.That(analysis.Url, Is.EqualTo(HtmlUrlFor(12345678)));
        });
    }

    [Test]
    public async Task AnalyzeWorkflowsAsync_ManyFailedRuns_ReturnsOneAnalysisPerRun()
    {
        GivenWorkflowRuns(
            WorkflowRunFor(1, "analyze"),
            WorkflowRunFor(2, "prepare-pipelines"),
            WorkflowRunFor(3, "event-processor"));

        var analyses = await helper.AnalyzeWorkflowsAsync(PrSource, CancellationToken.None);

        Assert.That(analyses.Select(a => a.Name), Is.EqualTo(new[] { "analyze", "prepare-pipelines", "event-processor" }));
    }

    [Test]
    public async Task AnalyzeWorkflowsAsync_RunWithLogs_ReturnsExtractedErrorLines()
    {
        GivenWorkflowRuns(WorkflowRunFor(12345678, "analyze"));
        GivenLogs(12345678, "##[error]Process completed with exit code 1.");
        logAnalysisHelper
            .Setup(l => l.AnalyzeLogContent(It.IsAny<TextReader>(), null, null, null, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LogEntry { Message = "Process completed with exit code 1." }]);

        var analysis = (await helper.AnalyzeWorkflowsAsync(PrSource, CancellationToken.None)).Single();

        Assert.That(analysis.Logs.Select(l => l.Message), Is.EqualTo(new[] { "Process completed with exit code 1." }));
    }

    [TestCase(null)]
    [TestCase("")]
    public async Task AnalyzeWorkflowsAsync_RunWithoutLogContent_SkipsLogAnalysis(string? logs)
    {
        GivenWorkflowRuns(WorkflowRunFor(12345678, "analyze"));
        GivenLogs(12345678, logs);

        var analysis = (await helper.AnalyzeWorkflowsAsync(PrSource, CancellationToken.None)).Single();

        Assert.That(analysis.Logs, Is.Empty);
        logAnalysisHelper.Verify(
            l => l.AnalyzeLogContent(It.IsAny<TextReader>(), It.IsAny<List<string>?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task AnalyzeWorkflowsAsync_LogReadFails_ReportsErrorAndStillReportsJobs()
    {
        GivenWorkflowRuns(WorkflowRunFor(12345678, "analyze"));
        gitHubService
            .Setup(g => g.GetWorkflowRunLogsAsync(Owner, Repo, 12345678, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("log archive expired", System.Net.HttpStatusCode.Gone));
        GivenJobs(12345678, WorkflowJobFor("build", WorkflowJobConclusion.Failure));

        var analysis = (await helper.AnalyzeWorkflowsAsync(PrSource, CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(analysis.Errors, Has.Exactly(1).Contains("Failed to read workflow run logs"));
            Assert.That(analysis.Jobs, Is.EqualTo(new[] { "build: failure" }));
        });
    }

    [Test]
    public async Task AnalyzeWorkflowsAsync_JobReadFails_ReportsErrorAndStillReportsLogs()
    {
        GivenWorkflowRuns(WorkflowRunFor(12345678, "analyze"));
        GivenLogs(12345678, "##[error]boom");
        logAnalysisHelper
            .Setup(l => l.AnalyzeLogContent(It.IsAny<TextReader>(), null, null, null, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LogEntry { Message = "boom" }]);
        gitHubService
            .Setup(g => g.GetWorkflowRunJobsAsync(Owner, Repo, 12345678, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("jobs unavailable", System.Net.HttpStatusCode.ServiceUnavailable));

        var analysis = (await helper.AnalyzeWorkflowsAsync(PrSource, CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(analysis.Errors, Has.Exactly(1).Contains("Failed to read workflow run jobs"));
            Assert.That(analysis.Logs.Select(l => l.Message), Is.EqualTo(new[] { "boom" }));
        });
    }

    [Test]
    public async Task AnalyzeWorkflowsAsync_JobWithoutConclusion_SummarizesJobByStatus()
    {
        GivenWorkflowRuns(WorkflowRunFor(12345678, "analyze"));
        GivenJobs(12345678, WorkflowJobFor("build", conclusion: null, status: WorkflowJobStatus.InProgress));

        var analysis = (await helper.AnalyzeWorkflowsAsync(PrSource, CancellationToken.None)).Single();

        Assert.That(analysis.Jobs, Is.EqualTo(new[] { "build: in_progress" }));
    }

    #endregion

    #region GetFailingChecksAsync

    [Test]
    public async Task GetFailingChecksAsync_SourceWithoutPullRequest_ReturnsEmptyWithoutQueryingChecks()
    {
        var checks = await helper.GetFailingChecksAsync(BranchSource, CancellationToken.None);

        Assert.That(checks, Is.Empty);
        gitHubService.Verify(
            g => g.GetPrCheckRunsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // The full set of non-passing conclusions lives in the helper; enumerating it here would only restate
    // data the helper already declares. One representative value per branch, plus the lower-case case that
    // proves the comparison ignores casing, is what actually has to hold.
    [TestCase("FAILURE")]
    [TestCase("failure", Description = "GitHub's REST API reports conclusions in lower case")]
    public async Task GetFailingChecksAsync_NonPassingConclusion_IncludesCheck(string conclusion)
    {
        GivenChecks(CheckRun("net - core - ci", conclusion));

        var checks = await helper.GetFailingChecksAsync(PrSource, CancellationToken.None);

        Assert.That(checks.Select(c => c.Name), Is.EqualTo(new[] { "net - core - ci" }));
    }

    [Test]
    public async Task GetFailingChecksAsync_MixedChecks_ReturnsOnlyTheFailingOnes()
    {
        GivenChecks(
            CheckRun("license/cla", "SUCCESS"),
            CheckRun("net - core - ci", "FAILURE"),
            CheckRun("net - template - ci", null),
            CheckRun("Publish Artifacts", "TIMED_OUT"));

        var checks = await helper.GetFailingChecksAsync(PrSource, CancellationToken.None);

        Assert.That(checks.Select(c => c.Name), Is.EqualTo(new[] { "net - core - ci", "Publish Artifacts" }));
    }

    #endregion

    #region Arrange helpers

    private void GivenWorkflowRuns(params WorkflowRun[] runs) =>
        gitHubService
            .Setup(g => g.GetFailedWorkflowRunsForCommitAsync(Owner, Repo, HeadSha, It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

    private void GivenLogs(long runId, string? logs) =>
        gitHubService
            .Setup(g => g.GetWorkflowRunLogsAsync(Owner, Repo, runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

    private void GivenJobs(long runId, params WorkflowJob[] jobs) =>
        gitHubService
            .Setup(g => g.GetWorkflowRunJobsAsync(Owner, Repo, runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

    private void GivenChecks(params PrCheckRun[] checks) =>
        gitHubService
            .Setup(g => g.GetPrCheckRunsAsync(Owner, Repo, PrNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(checks.ToList());

    private static PrCheckRun CheckRun(string name, string? conclusion) =>
        new() { Name = name, Conclusion = conclusion, Type = "CheckRun" };

    private static string HtmlUrlFor(long runId) => $"https://github.com/{Owner}/{Repo}/actions/runs/{runId}";

    /// <summary>
    /// Octokit response models only expose an all-argument constructor, so the fields under test are named
    /// and everything else is left at its default.
    /// </summary>
    private static WorkflowRun WorkflowRunFor(
        long id,
        string name,
        WorkflowRunStatus status = WorkflowRunStatus.Completed,
        WorkflowRunConclusion? conclusion = WorkflowRunConclusion.Failure) =>
        new(id, name, null!, 0, null!, "main", HeadSha, ".github/workflows/analyze.yml", 1, "pull_request",
            name, status, conclusion, 0, null!, HtmlUrlFor(id), null!, default, default, null!, 1, null!,
            default, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, 0);

    private static WorkflowJob WorkflowJobFor(
        string name,
        WorkflowJobConclusion? conclusion,
        WorkflowJobStatus status = WorkflowJobStatus.Completed) =>
        new(0, 0, null!, null!, HeadSha, null!, null!, status, conclusion, default, default, default,
            name, null!, null!, null!);

    #endregion
}
