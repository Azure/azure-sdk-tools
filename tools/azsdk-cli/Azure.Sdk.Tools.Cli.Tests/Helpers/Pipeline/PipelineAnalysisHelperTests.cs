// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Helpers.Pipeline;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.TeamFoundation.Build.WebApi;
using Moq;
using Newtonsoft.Json;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Pipeline;

/// <summary>
/// Seam: <see cref="PipelineAnalysisHelper"/> turns already-resolved builds into per-build log and test
/// findings, given <see cref="IDevOpsService"/>, <see cref="ILogAnalysisHelper"/>, and
/// <see cref="ITestResultParserResolver"/>.
/// </summary>
[TestFixture]
public class PipelineAnalysisHelperTests
{
    // Shapes taken from a real azure-sdk-for-net public build.
    private const int BuildId = 5209385;
    private const string Project = "public";
    private const string PipelineUrl = "https://dev.azure.com/azure-sdk/public/_build/results?buildId=5209385";
    private const string Platform = "Ubuntu2404_NET80_PackageRef_Debug";
    private const int FailedTaskLogId = 42;

    private static readonly AzurePipelineBuild Build = new(BuildId, Project, PipelineUrl, "completed", "failed");

    private Mock<IDevOpsService> devOpsService;
    private Mock<ILogAnalysisHelper> logAnalysisHelper;
    private Mock<ITestResultParserResolver> parserResolver;
    private PipelineAnalysisHelper helper;

    /// <summary>What the log analyzer reports for every log it is given.</summary>
    private List<LogEntry> logAnalyzerResult;

    [SetUp]
    public void SetUp()
    {
        logAnalyzerResult = [];

        devOpsService = new Mock<IDevOpsService>(MockBehavior.Loose);
        devOpsService
            .Setup(d => d.GetBuildTimelineAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TimelineOf());
        devOpsService
            .Setup(d => d.GetBuildLogLinesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        devOpsService
            .Setup(d => d.GetPipelineLlmArtifacts(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        logAnalysisHelper = new Mock<ILogAnalysisHelper>(MockBehavior.Loose);
        logAnalysisHelper
            .Setup(l => l.AnalyzeLogContent(It.IsAny<string>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => logAnalyzerResult);

        parserResolver = new Mock<ITestResultParserResolver>(MockBehavior.Loose);

        helper = new PipelineAnalysisHelper(
            devOpsService.Object,
            logAnalysisHelper.Object,
            parserResolver.Object,
            new TestLogger<PipelineAnalysisHelper>());
    }

    #region AnalyzePipelineAsync - which logs get analyzed

    [Test]
    public async Task AnalyzePipelineAsync_ManyBuilds_ReturnsOneAnalysisPerBuild()
    {
        var (analyses, _) = await helper.AnalyzePipelineAsync(
            [Build, Build with { BuildId = 5209386 }, Build with { BuildId = 5209387 }]);

        Assert.That(analyses.Select(a => a.PipelineBuild.BuildId), Is.EqualTo(new[] { 5209385, 5209386, 5209387 }));
    }

    [Test]
    public async Task AnalyzePipelineAsync_ExplicitLogId_AnalyzesThatLogWithoutReadingTheTimeline()
    {
        await helper.AnalyzePipelineAsync([Build], logId: 99);

        devOpsService.Verify(
            d => d.GetBuildLogLinesAsync(Project, BuildId, 99, It.IsAny<CancellationToken>()), Times.Once);
        devOpsService.Verify(
            d => d.GetBuildTimelineAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task AnalyzePipelineAsync_LogIdZero_FallsBackToTheTimeline()
    {
        GivenTimeline(FailedTask("Build", FailedTaskLogId));

        await helper.AnalyzePipelineAsync([Build], logId: 0);

        devOpsService.Verify(
            d => d.GetBuildLogLinesAsync(Project, BuildId, FailedTaskLogId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AnalyzePipelineAsync_FailedTask_AnalyzesItsLog()
    {
        GivenTimeline(FailedTask("Build", FailedTaskLogId));

        await helper.AnalyzePipelineAsync([Build]);

        devOpsService.Verify(
            d => d.GetBuildLogLinesAsync(Project, BuildId, FailedTaskLogId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AnalyzePipelineAsync_SucceededTask_IsNotAnalyzed()
    {
        GivenTimeline(Record("Build", FailedTaskLogId, TaskResult.Succeeded, "Task"));

        await helper.AnalyzePipelineAsync([Build]);

        ThenNoLogsWereDownloaded();
    }

    [Test]
    public async Task AnalyzePipelineAsync_FailedRecordThatIsNotATask_IsNotAnalyzed()
    {
        GivenTimeline(Record("Build", FailedTaskLogId, TaskResult.Failed, "Job"));

        await helper.AnalyzePipelineAsync([Build]);

        ThenNoLogsWereDownloaded();
    }

    [Test]
    public async Task AnalyzePipelineAsync_FailedTestStepWithTestResults_IsNotAnalyzed()
    {
        GivenTimeline(FailedTask("Run Tests", FailedTaskLogId));
        GivenRecoveredTestResults();

        await helper.AnalyzePipelineAsync([Build]);

        ThenNoLogsWereDownloaded();
    }

    /// <summary>
    /// A test step also restores and compiles. When it fails there no test results are produced, so skipping
    /// its log the way <see cref="AnalyzePipelineAsync_FailedTestStepWithTestResults_IsNotAnalyzed"/> expects
    /// would leave the build with nothing at all to explain it.
    /// </summary>
    [Test]
    public async Task AnalyzePipelineAsync_FailedTestStepWithoutTestResults_IsAnalyzedAnyway()
    {
        GivenTimeline(FailedTask("Run Tests", FailedTaskLogId));

        await helper.AnalyzePipelineAsync([Build]);

        devOpsService.Verify(
            d => d.GetBuildLogLinesAsync(Project, BuildId, FailedTaskLogId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AnalyzePipelineAsync_FailedTestStepWhoseTestResultsCannotBeRead_IsAnalyzedAnyway()
    {
        GivenTimeline(FailedTask("Run Tests", FailedTaskLogId));
        devOpsService
            .Setup(d => d.GetPipelineLlmArtifacts(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("artifact feed unavailable"));

        await helper.AnalyzePipelineAsync([Build]);

        devOpsService.Verify(
            d => d.GetBuildLogLinesAsync(Project, BuildId, FailedTaskLogId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AnalyzePipelineAsync_FailedDeployTestResourcesStep_IsAnalyzed()
    {
        GivenTimeline(FailedTask("Deploy test resources", FailedTaskLogId));
        GivenRecoveredTestResults();

        await helper.AnalyzePipelineAsync([Build]);

        devOpsService.Verify(
            d => d.GetBuildLogLinesAsync(Project, BuildId, FailedTaskLogId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AnalyzePipelineAsync_FailedTaskWithoutALog_IsNotAnalyzed()
    {
        GivenTimeline(Record("Build", logId: null, TaskResult.Failed, "Task"));

        await helper.AnalyzePipelineAsync([Build]);

        ThenNoLogsWereDownloaded();
    }

    [Test]
    public async Task AnalyzePipelineAsync_FailedTasksSharingALog_AnalyzeThatLogOnce()
    {
        GivenTimeline(FailedTask("Build", FailedTaskLogId), FailedTask("Pack", FailedTaskLogId));

        await helper.AnalyzePipelineAsync([Build]);

        devOpsService.Verify(
            d => d.GetBuildLogLinesAsync(Project, BuildId, FailedTaskLogId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region AnalyzePipelineAsync - how findings are reported

    [Test]
    public async Task AnalyzePipelineAsync_LogsContainErrors_ReportsThemAsFailedPipelineTasks()
    {
        GivenTimeline(FailedTask("Build", FailedTaskLogId));
        logAnalyzerResult = [new LogEntry { Message = "error CS0246: The type or namespace name 'Foo' could not be found" }];

        var (analyses, _) = await helper.AnalyzePipelineAsync([Build]);

        Assert.That(
            analyses.Single().FailedPipelineTasks!.Errors.Select(e => e.Message),
            Is.EqualTo(logAnalyzerResult.Select(e => e.Message)));
    }

    [Test]
    public async Task AnalyzePipelineAsync_LogsContainNoErrors_LeavesFailedPipelineTasksEmpty()
    {
        GivenTimeline(FailedTask("Build", FailedTaskLogId));

        var (analyses, _) = await helper.AnalyzePipelineAsync([Build]);

        Assert.Multiple(() =>
        {
            Assert.That(analyses.Single().FailedPipelineTasks, Is.Null);
            Assert.That(analyses.Single().Errors, Is.Null);
        });
    }

    [Test]
    public async Task AnalyzePipelineAsync_LogDownloadFails_ReportsErrorOnTheBuild()
    {
        GivenTimeline(FailedTask("Build", FailedTaskLogId));
        devOpsService
            .Setup(d => d.GetBuildLogLinesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("log has been deleted"));

        var (analyses, _) = await helper.AnalyzePipelineAsync([Build]);

        Assert.That(analyses.Single().Errors, Has.Exactly(1).Contains("Failed to analyze pipeline 5209385"));
    }

    [Test]
    public async Task AnalyzePipelineAsync_TimelineCannotBeRead_ReportsErrorOnTheBuild()
    {
        devOpsService
            .Setup(d => d.GetBuildTimelineAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("no access to the internal project"));

        var (analyses, _) = await helper.AnalyzePipelineAsync([Build]);

        Assert.That(analyses.Single().Errors, Has.Exactly(1).Contains("Failed to analyze failure logs"));
    }

    #endregion

    #region AnalyzePipelineAsync - test artifacts

    [Test]
    public async Task AnalyzePipelineAsync_TestArtifactParses_ReportsItsFailedTests()
    {
        GivenTestArtifacts("test-results.trx");
        GivenParsedFailures("test-results.trx", "Azure.Core.Tests.PipelineTests.CanRetry");

        var (analyses, warnings) = await helper.AnalyzePipelineAsync([Build]);

        var artifact = analyses.Single().FailedPipelineTests!.Single();
        Assert.Multiple(() =>
        {
            Assert.That(artifact.ArtifactFilePath, Is.EqualTo("test-results.trx"));
            Assert.That(artifact.Platform, Is.EqualTo(Platform));
            Assert.That(
                artifact.FailedTestTitles,
                Is.EqualTo(new[] { "Azure.Core.Tests.PipelineTests.CanRetry" }));
            Assert.That(warnings, Is.Empty);
        });
    }

    [Test]
    public async Task AnalyzePipelineAsync_TestArtifactCannotBeParsed_ReportsAWarningNamingIt()
    {
        GivenTestArtifacts("test-results.trx");
        parserResolver
            .Setup(p => p.ResolveAsync("test-results.trx", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("no registered parser recognizes this format"));

        var (analyses, warnings) = await helper.AnalyzePipelineAsync([Build]);

        Assert.Multiple(() =>
        {
            Assert.That(warnings, Has.Exactly(1).Contains("test-results.trx"));
            Assert.That(analyses.Single().FailedPipelineTests, Is.Null);
        });
    }

    [Test]
    public async Task AnalyzePipelineAsync_OneUnparsableArtifact_DoesNotDiscardTheOthers()
    {
        GivenTestArtifacts("broken.xml", "test-results.trx");
        parserResolver
            .Setup(p => p.ResolveAsync("broken.xml", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("malformed content"));
        GivenParsedFailures("test-results.trx", "Azure.Core.Tests.PipelineTests.CanRetry");

        var (analyses, _) = await helper.AnalyzePipelineAsync([Build]);

        Assert.That(
            analyses.Single().FailedPipelineTests!.SelectMany(a => a.FailedTestTitles),
            Is.EqualTo(new[] { "Azure.Core.Tests.PipelineTests.CanRetry" }));
    }

    [Test]
    public async Task AnalyzePipelineAsync_ArtifactsFromManyPlatforms_KeepsEachPlatformsFailuresSeparate()
    {
        GivenTestArtifacts(new Dictionary<string, List<string>>
        {
            ["Ubuntu2404_NET80"] = ["linux.trx"],
            ["Windows2022_NET80"] = ["windows.trx"],
        });
        GivenParsedFailures("linux.trx", "Azure.Core.Tests.PipelineTests.CanRetry");
        GivenParsedFailures("windows.trx", "Azure.Core.Tests.PipelineTests.HonorsTimeout");

        var (analyses, _) = await helper.AnalyzePipelineAsync([Build]);

        var failedTests = analyses.Single().FailedPipelineTests!;
        Assert.Multiple(() =>
        {
            Assert.That(
                failedTests.Single(a => a.Platform == "Ubuntu2404_NET80").FailedTestTitles,
                Is.EqualTo(new[] { "Azure.Core.Tests.PipelineTests.CanRetry" }));
            Assert.That(
                failedTests.Single(a => a.Platform == "Windows2022_NET80").FailedTestTitles,
                Is.EqualTo(new[] { "Azure.Core.Tests.PipelineTests.HonorsTimeout" }));
        });
    }

    [Test]
    public async Task AnalyzePipelineAsync_PlatformWithNoFailures_IsNotGivenAnEntry()
    {
        GivenTestArtifacts(new Dictionary<string, List<string>>
        {
            ["Ubuntu2404_NET80"] = ["linux.trx"],
            ["Windows2022_NET80"] = ["windows.trx"],
        });
        GivenParsedFailures("linux.trx", "Azure.Core.Tests.PipelineTests.CanRetry");
        GivenParsedFailures("windows.trx");

        var (analyses, _) = await helper.AnalyzePipelineAsync([Build]);

        Assert.That(
            analyses.Single().FailedPipelineTests!.Select(a => a.Platform),
            Is.EqualTo(new[] { "Ubuntu2404_NET80" }));
    }

    [Test]
    public async Task AnalyzePipelineAsync_TestArtifactLookupFails_ReportsErrorOnTheBuild()
    {
        devOpsService
            .Setup(d => d.GetPipelineLlmArtifacts(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("artifact feed unavailable"));

        var (analyses, _) = await helper.AnalyzePipelineAsync([Build]);

        Assert.That(analyses.Single().Errors, Has.Exactly(1).Contains("Failed to analyze test artifacts"));
    }

    [Test]
    public async Task AnalyzePipelineAsync_OneBuildFailingToAnalyze_DoesNotStopTheNext()
    {
        devOpsService
            .Setup(d => d.GetBuildTimelineAsync(Project, BuildId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("no access"));
        GivenTestArtifacts("test-results.trx");
        GivenParsedFailures("test-results.trx", "Azure.Core.Tests.PipelineTests.CanRetry");

        var (analyses, _) = await helper.AnalyzePipelineAsync([Build, Build with { BuildId = 5209386 }]);

        Assert.Multiple(() =>
        {
            Assert.That(analyses[0].Errors, Is.Not.Null);
            Assert.That(analyses[1].Errors, Is.Null);
            Assert.That(analyses[1].FailedPipelineTests, Has.Count.EqualTo(1));
        });
    }

    #endregion

    #region AnalyzePipelineFailureLogsAsync

    [Test]
    public async Task AnalyzePipelineFailureLogsAsync_NoLogIds_ReturnsNoErrorsForThePipeline()
    {
        var response = await helper.AnalyzePipelineFailureLogsAsync(Build, [], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(response.Errors, Is.Empty);
            Assert.That(response.PipelineUrl, Is.EqualTo(PipelineUrl));
            Assert.That(response.ResponseError, Is.Null.Or.Empty);
        });
    }

    [Test]
    public async Task AnalyzePipelineFailureLogsAsync_ManyLogs_AggregatesTheErrorsOfEach()
    {
        logAnalyzerResult = [new LogEntry { Message = "error CS0246" }];

        var response = await helper.AnalyzePipelineFailureLogsAsync(Build, [42, 43], CancellationToken.None);

        Assert.That(response.Errors, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task AnalyzePipelineFailureLogsAsync_LogDownloadFails_ReturnsResponseError()
    {
        devOpsService
            .Setup(d => d.GetBuildLogLinesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("log has been deleted"));

        var response = await helper.AnalyzePipelineFailureLogsAsync(Build, [42], CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("Failed to analyze pipeline 5209385"));
    }

    #endregion

    #region Arrange helpers

    private void GivenTimeline(params TimelineRecord[] records) =>
        devOpsService
            .Setup(d => d.GetBuildTimelineAsync(Project, BuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TimelineOf(records));

    private void GivenTestArtifacts(params string[] files) =>
        GivenTestArtifacts(new Dictionary<string, List<string>> { [Platform] = [.. files] });

    private void GivenTestArtifacts(Dictionary<string, List<string>> artifactsByPlatform) =>
        devOpsService
            .Setup(d => d.GetPipelineLlmArtifacts(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(artifactsByPlatform);

    private void GivenParsedFailures(string file, params string[] testCaseTitles)
    {
        var parser = new Mock<ITestHelper>(MockBehavior.Loose);
        parser
            .Setup(p => p.GetFailedTestCases(file, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FailedTestRunListResponse
            {
                Items = [.. testCaseTitles.Select(title => new FailedTestRunResponse { TestCaseTitle = title })]
            });
        parserResolver
            .Setup(p => p.ResolveAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parser.Object);
    }

    /// <summary>A build whose test results were recovered, which is what suppresses test-step log analysis.</summary>
    private void GivenRecoveredTestResults()
    {
        GivenTestArtifacts("test-results.trx");
        GivenParsedFailures("test-results.trx", "Azure.Core.Tests.PipelineTests.CanRetry");
    }

    private void ThenNoLogsWereDownloaded() =>
        devOpsService.Verify(
            d => d.GetBuildLogLinesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

    /// <summary>
    /// <see cref="Timeline"/> has no public constructor; the DevOps client only ever materializes it from the
    /// REST payload, so the tests do the same.
    /// </summary>
    private static Timeline TimelineOf(params TimelineRecord[] records)
    {
        var timeline = JsonConvert.DeserializeObject<Timeline>("""{ "records": [] }""")!;
        timeline.Records.AddRange(records);
        return timeline;
    }

    private static TimelineRecord FailedTask(string name, int logId) =>
        Record(name, logId, TaskResult.Failed, "Task");

    private static TimelineRecord Record(string name, int? logId, TaskResult result, string recordType) =>
        new()
        {
            Name = name,
            Result = result,
            RecordType = recordType,
            Log = logId == null ? null : new BuildLogReference { Id = logId.Value },
        };

    #endregion
}
