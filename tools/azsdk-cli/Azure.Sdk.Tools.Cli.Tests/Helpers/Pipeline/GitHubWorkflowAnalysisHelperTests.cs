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
/// Seam: <see cref="GitHubWorkflowAnalysisHelper"/> turns a resolved <see cref="GitHubCommitRef"/> into
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

    private static readonly GitHubCommitRef PrCommitRef = new(Owner, Repo, HeadSha, PrNumber);
    private static readonly GitHubCommitRef BranchCommitRef = new(Owner, Repo, HeadSha, null);

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

        // A run that published no logs is the common case in these tests, and a loose mock would otherwise
        // hand back a null list where the service contract promises an empty one.
        gitHubService
            .Setup(g => g.GetFailedWorkflowRunLogsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(string Name, string Content)>());
    }

    #region AnalyzeWorkflowsAsync

    [Test]
    public async Task AnalyzeWorkflowsAsync_Always_RequestsFailedRunsForTheSourceCommit()
    {
        gitHubService
            .Setup(g => g.GetFailedWorkflowRunsForCommitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WorkflowRun>());

        await helper.AnalyzeWorkflowsAsync(PrCommitRef, CancellationToken.None);

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

        var analysis = (await helper.AnalyzeWorkflowsAsync(PrCommitRef, CancellationToken.None)).Single();

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

        var analyses = await helper.AnalyzeWorkflowsAsync(PrCommitRef, CancellationToken.None);

        Assert.That(analyses.Select(a => a.Name), Is.EqualTo(new[] { "analyze", "prepare-pipelines", "event-processor" }));
    }

    // A commit routinely carries more than one failed run of the same workflow, and their logs are the same
    // failure reported twice.
    [Test]
    public async Task AnalyzeWorkflowsAsync_ManyRunsOfOneWorkflow_AnalyzesOnlyTheLatest()
    {
        GivenWorkflowRuns(
            WorkflowRunFor(1, "analyze", workflowId: 42, createdAt: new DateTimeOffset(2026, 7, 28, 18, 0, 0, TimeSpan.Zero)),
            WorkflowRunFor(2, "analyze", workflowId: 42, createdAt: new DateTimeOffset(2026, 7, 28, 19, 0, 0, TimeSpan.Zero)));

        var analyses = await helper.AnalyzeWorkflowsAsync(PrCommitRef, CancellationToken.None);

        Assert.That(analyses.Select(a => a.Url), Is.EqualTo(new[] { HtmlUrlFor(2) }));
    }

    [Test]
    public async Task AnalyzeWorkflowsAsync_RunWithLogs_ReturnsExtractedErrorLines()
    {
        GivenWorkflowRuns(WorkflowRunFor(12345678, "analyze"));
        GivenLogs(12345678, ("analyze/3_Build.txt", "##[error]Process completed with exit code 1."));
        logAnalysisHelper
            .Setup(l => l.AnalyzeLogContent(It.IsAny<TextReader>(), It.IsAny<List<string>?>(), null, null, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LogEntry { Message = "Process completed with exit code 1." }]);

        var analysis = (await helper.AnalyzeWorkflowsAsync(PrCommitRef, CancellationToken.None)).Single();

        Assert.That(analysis.Logs.Select(l => l.Message), Is.EqualTo(new[] { "Process completed with exit code 1." }));
    }

    // Each log file is analyzed on its own, so the name of the file an error came from is available to the
    // analysis rather than being folded into one concatenated blob.
    [Test]
    public async Task AnalyzeWorkflowsAsync_ManyLogFiles_AnalyzesEachOneByName()
    {
        GivenWorkflowRuns(WorkflowRunFor(12345678, "analyze"));
        GivenLogs(
            12345678,
            ("analyze/3_Build.txt", "##[error]build failed"),
            ("analyze/4_Test.txt", "##[error]test failed"));
        logAnalysisHelper
            .Setup(l => l.AnalyzeLogContent(It.IsAny<TextReader>(), It.IsAny<List<string>?>(), null, null, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TextReader _, List<string>? _, int? _, int? _, string _, string filePath, CancellationToken _) =>
                [new LogEntry { File = filePath }]);

        var analysis = (await helper.AnalyzeWorkflowsAsync(PrCommitRef, CancellationToken.None)).Single();

        Assert.That(analysis.Logs.Select(l => l.File), Is.EqualTo(new[] { "analyze/3_Build.txt", "analyze/4_Test.txt" }));
    }

    [Test]
    public async Task AnalyzeWorkflowsAsync_RunWithoutLogContent_SkipsLogAnalysis()
    {
        GivenWorkflowRuns(WorkflowRunFor(12345678, "analyze"));
        GivenLogs(12345678);

        var analysis = (await helper.AnalyzeWorkflowsAsync(PrCommitRef, CancellationToken.None)).Single();

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
            .Setup(g => g.GetFailedWorkflowRunLogsAsync(Owner, Repo, 12345678, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("log archive expired", System.Net.HttpStatusCode.Gone));
        GivenJobs(12345678, WorkflowJobFor("build", WorkflowJobConclusion.Failure));

        var analysis = (await helper.AnalyzeWorkflowsAsync(PrCommitRef, CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(analysis.Errors, Has.Exactly(1).Contains("Failed to read workflow run logs"));
            Assert.That(analysis.Jobs, Is.EqualTo(new[] { "build: failure" }));
        });
    }

    [Test]
    public async Task AnalyzeWorkflowsAsync_RunWithLogs_DoesNotListJobs()
    {
        GivenWorkflowRuns(WorkflowRunFor(12345678, "analyze"));
        GivenLogs(12345678, ("analyze/3_Build.txt", "##[error]boom"));
        logAnalysisHelper
            .Setup(l => l.AnalyzeLogContent(It.IsAny<TextReader>(), It.IsAny<List<string>?>(), null, null, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LogEntry { Message = "boom" }]);

        var analysis = (await helper.AnalyzeWorkflowsAsync(PrCommitRef, CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(analysis.Logs.Select(l => l.Message), Is.EqualTo(new[] { "boom" }));
            Assert.That(analysis.Jobs, Is.Empty);
        });
        gitHubService.Verify(
            g => g.GetWorkflowRunJobsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task AnalyzeWorkflowsAsync_JobReadFailsWithoutLogs_ReportsError()
    {
        GivenWorkflowRuns(WorkflowRunFor(12345678, "analyze"));
        GivenLogs(12345678);
        gitHubService
            .Setup(g => g.GetWorkflowRunJobsAsync(Owner, Repo, 12345678, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("jobs unavailable", System.Net.HttpStatusCode.ServiceUnavailable));

        var analysis = (await helper.AnalyzeWorkflowsAsync(PrCommitRef, CancellationToken.None)).Single();

        Assert.That(analysis.Errors, Has.Exactly(1).Contains("Failed to read workflow run jobs"));
    }

    [Test]
    public async Task AnalyzeWorkflowsAsync_JobWithoutConclusion_SummarizesJobByStatus()
    {
        GivenWorkflowRuns(WorkflowRunFor(12345678, "analyze"));
        GivenJobs(12345678, WorkflowJobFor("build", conclusion: null, status: WorkflowJobStatus.InProgress));

        var analysis = (await helper.AnalyzeWorkflowsAsync(PrCommitRef, CancellationToken.None)).Single();

        Assert.That(analysis.Jobs, Is.EqualTo(new[] { "build: in_progress" }));
    }

    #endregion

    #region GetFailingChecksAsync

    [Test]
    public async Task GetFailingChecksAsync_CommitRefWithoutPullRequest_ReturnsEmptyWithoutQueryingChecks()
    {
        var checks = await helper.GetFailingChecksAsync(BranchCommitRef, CancellationToken.None);

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

        var checks = await helper.GetFailingChecksAsync(PrCommitRef, CancellationToken.None);

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

        var checks = await helper.GetFailingChecksAsync(PrCommitRef, CancellationToken.None);

        Assert.That(checks.Select(c => c.Name), Is.EqualTo(new[] { "net - core - ci", "Publish Artifacts" }));
    }

    #endregion

    #region Arrange helpers

    private void GivenWorkflowRuns(params WorkflowRun[] runs) =>
        gitHubService
            .Setup(g => g.GetFailedWorkflowRunsForCommitAsync(Owner, Repo, HeadSha, It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

    private void GivenLogs(long runId, params (string Name, string Content)[] logs) =>
        gitHubService
            .Setup(g => g.GetFailedWorkflowRunLogsAsync(Owner, Repo, runId, It.IsAny<CancellationToken>()))
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
        WorkflowRunConclusion? conclusion = WorkflowRunConclusion.Failure,
        long? workflowId = null,
        DateTimeOffset createdAt = default) =>
        new(id, name, null!, 0, null!, "main", HeadSha, ".github/workflows/analyze.yml", 1, "pull_request",
            name, status, conclusion, workflowId ?? id, null!, HtmlUrlFor(id), null!, createdAt, default, null!, 1, null!,
            default, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, 0);

    private static WorkflowJob WorkflowJobFor(
        string name,
        WorkflowJobConclusion? conclusion,
        WorkflowJobStatus status = WorkflowJobStatus.Completed) =>
        new(0, 0, null!, null!, HeadSha, null!, null!, status, conclusion, default, default, default,
            name, null!, null!, null!);

    #endregion
}
