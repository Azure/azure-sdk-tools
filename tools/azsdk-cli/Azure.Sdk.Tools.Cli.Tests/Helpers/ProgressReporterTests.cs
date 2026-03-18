// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Diagnostics;
using ModelContextProtocol;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class ProgressReporterTests
{
    private TestLogger<ProgressReporterTests> _logger;
    private Mock<IRawOutputHelper> _mockOutputHelper;
    private List<string> _consoleOutput;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<ProgressReporterTests>();
        _consoleOutput = new List<string>();
        _mockOutputHelper = new Mock<IRawOutputHelper>();
        _mockOutputHelper.Setup(o => o.OutputConsoleInfo(It.IsAny<string>()))
            .Callback<string>(s => _consoleOutput.Add(s));
    }

    #region Constructor validation

    [Test]
    public void Constructor_ZeroSteps_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProgressReporter(null, _logger, totalSteps: 0, _mockOutputHelper.Object));
    }

    [Test]
    public void Constructor_NegativeSteps_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProgressReporter(null, _logger, totalSteps: -1, _mockOutputHelper.Object));
    }

    #endregion

    #region NextStep tests

    [Test]
    public void NextStep_ReportsStepsSequentially()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 3, _mockOutputHelper.Object);

        reporter.NextStep("Step one");
        reporter.NextStep("Step two");
        reporter.NextStep("Step three");

        Assert.That(_consoleOutput, Has.Count.EqualTo(3));
        Assert.That(_consoleOutput[0], Does.Contain("Step one"));
        Assert.That(_consoleOutput[1], Does.Contain("Step two"));
        Assert.That(_consoleOutput[2], Does.Contain("Step three"));
    }

    [Test]
    public void NextStep_ExceedsTotalSteps_Throws()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 2, _mockOutputHelper.Object);

        reporter.NextStep("Step one");
        reporter.NextStep("Step two");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            reporter.NextStep("Step three"));
        Assert.That(ex!.Message, Does.Contain("more steps"));
    }

    [Test]
    public void NextStep_WithProgress_ReportsCorrectValues()
    {
        var reported = new List<ProgressNotificationValue>();
        var mockProgress = new Mock<IProgress<ProgressNotificationValue>>();
        mockProgress.Setup(p => p.Report(It.IsAny<ProgressNotificationValue>()))
            .Callback<ProgressNotificationValue>(v => reported.Add(v));

        var reporter = new ProgressReporter(mockProgress.Object, _logger, totalSteps: 3, _mockOutputHelper.Object);

        reporter.NextStep("First");
        reporter.NextStep("Second");
        reporter.NextStep("Third");

        Assert.That(reported, Has.Count.EqualTo(3));

        Assert.That(reported[0].Progress, Is.EqualTo(1));
        Assert.That(reported[0].Total, Is.EqualTo(3));
        Assert.That(reported[0].Message, Is.EqualTo("First"));

        Assert.That(reported[1].Progress, Is.EqualTo(2));
        Assert.That(reported[1].Total, Is.EqualTo(3));
        Assert.That(reported[1].Message, Is.EqualTo("Second"));

        Assert.That(reported[2].Progress, Is.EqualTo(3));
        Assert.That(reported[2].Total, Is.EqualTo(3));
        Assert.That(reported[2].Message, Is.EqualTo("Third"));
    }

    [Test]
    public void NextStep_NoProgress_FallsBackToLogger()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 1, _mockOutputHelper.Object);

        reporter.NextStep("test message");

        Assert.That(_consoleOutput, Has.Count.EqualTo(1));
        Assert.That(_consoleOutput[0], Does.Contain("test message"));
    }

    #endregion

    #region EnsureComplete tests

    [Test]
    public void EnsureComplete_AllStepsReported_DoesNotThrow()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 2, _mockOutputHelper.Object);

        reporter.NextStep("Step one");
        reporter.NextStep("Step two");

        Assert.DoesNotThrow(() => reporter.EnsureComplete());
    }

    [Test]
    public void EnsureComplete_MissingSteps_Throws()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 3, _mockOutputHelper.Object);

        reporter.NextStep("Step one");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            reporter.EnsureComplete());
        Assert.That(ex!.Message, Does.Contain("Expected 3"));
        Assert.That(ex.Message, Does.Contain("only 1"));
    }

    #endregion

    #region Heartbeat tests

    private static async Task DelayWithRetry<T>(List<T> counter, int maxRetries)
    {
        var timeout = TimeSpan.FromMilliseconds(100);
        var sw = Stopwatch.StartNew();
        while (counter.Count < maxRetries && sw.Elapsed < timeout)
        {
            await Task.Delay(10);
        }
    }

    [Test]
    public async Task StartHeartbeat_EmitsHeartbeatsUntilDisposed()
    {
        var reported = new List<ProgressNotificationValue>();
        var mockProgress = new Mock<IProgress<ProgressNotificationValue>>();
        mockProgress.Setup(p => p.Report(It.IsAny<ProgressNotificationValue>()))
            .Callback<ProgressNotificationValue>(v => reported.Add(v));

        var reporter = new ProgressReporter(mockProgress.Object, _logger, totalSteps: 2, _mockOutputHelper.Object);

        reporter.NextStep("Starting work");

        await using (reporter.StartHeartbeat("Working", heartbeatInterval: TimeSpan.FromMilliseconds(5)))
        {
            await DelayWithRetry(reported, 2);
        }

        // Should have initial step report + at least 1 heartbeat
        Assert.That(reported.Count, Is.GreaterThanOrEqualTo(2));

        // First report is from NextStep
        Assert.That(reported[0].Message, Is.EqualTo("Starting work"));

        // Heartbeat messages include elapsed time
        Assert.That(reported[1].Message, Does.Contain("elapsed"));
    }

    [Test]
    public async Task StartHeartbeat_StopsAfterDispose()
    {
        var reported = new List<ProgressNotificationValue>();
        var mockProgress = new Mock<IProgress<ProgressNotificationValue>>();
        mockProgress.Setup(p => p.Report(It.IsAny<ProgressNotificationValue>()))
            .Callback<ProgressNotificationValue>(v => reported.Add(v));

        var reporter = new ProgressReporter(mockProgress.Object, _logger, totalSteps: 2, _mockOutputHelper.Object);

        reporter.NextStep("Step 1");

        await using (reporter.StartHeartbeat("Working", heartbeatInterval: TimeSpan.FromMilliseconds(5)))
        {
            await DelayWithRetry(reported, 2);
        }

        var countAfterDispose = reported.Count;

        // Wait a bit more — no new heartbeats should appear
        await Task.Delay(50);

        Assert.That(reported.Count, Is.EqualTo(countAfterDispose));
    }

    [Test]
    public async Task StartHeartbeat_NoProgress_FallsBackToLogger()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 2, _mockOutputHelper.Object);

        reporter.NextStep("Step 1");

        await using (reporter.StartHeartbeat("Working", heartbeatInterval: TimeSpan.FromMilliseconds(5)))
        {
            await DelayWithRetry(_consoleOutput, 2);
        }

        // At least the step message + some heartbeats
        Assert.That(_consoleOutput.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(_consoleOutput[0], Does.Contain("Step 1"));
    }

    [Test]
    public async Task StartHeartbeat_Cancellation_StopsHeartbeat()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 2, _mockOutputHelper.Object);
        reporter.NextStep("Step 1");

        using var cts = new CancellationTokenSource();

        var heartbeat = reporter.StartHeartbeat("Working",
            ct: cts.Token,
            heartbeatInterval: TimeSpan.FromMilliseconds(5));

        await Task.Delay(20);
        cts.Cancel();

        // DisposeAsync should complete without throwing
        await heartbeat.DisposeAsync();

        var countAfterCancel = _consoleOutput.Count;
        await Task.Delay(50);

        Assert.That(_consoleOutput.Count, Is.EqualTo(countAfterCancel));
    }

    [Test]
    public async Task StartHeartbeat_FastTask_NoHeartbeats()
    {
        var reported = new List<ProgressNotificationValue>();
        var mockProgress = new Mock<IProgress<ProgressNotificationValue>>();
        mockProgress.Setup(p => p.Report(It.IsAny<ProgressNotificationValue>()))
            .Callback<ProgressNotificationValue>(v => reported.Add(v));

        var reporter = new ProgressReporter(mockProgress.Object, _logger, totalSteps: 2, _mockOutputHelper.Object);

        reporter.NextStep("Fast step");

        await using (reporter.StartHeartbeat("Working", heartbeatInterval: TimeSpan.FromSeconds(60)))
        {
            // Task completes immediately — no heartbeats should fire
        }

        // Only the initial NextStep report
        Assert.That(reported, Has.Count.EqualTo(1));
        Assert.That(reported[0].Message, Is.EqualTo("Fast step"));
    }

    [Test]
    public async Task StartHeartbeat_ReportsCorrectStepIndex()
    {
        var reported = new List<ProgressNotificationValue>();
        var mockProgress = new Mock<IProgress<ProgressNotificationValue>>();
        mockProgress.Setup(p => p.Report(It.IsAny<ProgressNotificationValue>()))
            .Callback<ProgressNotificationValue>(v => reported.Add(v));

        var reporter = new ProgressReporter(mockProgress.Object, _logger, totalSteps: 3, _mockOutputHelper.Object);

        reporter.NextStep("Step 1"); // progress = 1
        reporter.NextStep("Step 2"); // progress = 2

        await using (reporter.StartHeartbeat("Working", heartbeatInterval: TimeSpan.FromMilliseconds(5)))
        {
            await DelayWithRetry(reported, 3);
        }

        // Heartbeat messages should use step index 2 (current step after NextStep increments)
        var heartbeats = reported.Where(r => r.Message!.Contains("elapsed")).ToList();
        Assert.That(heartbeats.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(heartbeats.All(h => h.Progress == 2 && h.Total == 3), Is.True);
    }

    #endregion

    #region CurrentStep / TotalSteps properties

    [Test]
    public void CurrentStep_TracksProgress()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 3, _mockOutputHelper.Object);
        Assert.That(reporter.CurrentStep, Is.EqualTo(0));

        reporter.NextStep("First");
        Assert.That(reporter.CurrentStep, Is.EqualTo(1));

        reporter.NextStep("Second");
        Assert.That(reporter.CurrentStep, Is.EqualTo(2));
    }

    [Test]
    public void TotalSteps_ReturnsConstructorValue()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 5, _mockOutputHelper.Object);
        Assert.That(reporter.TotalSteps, Is.EqualTo(5));
    }

    #endregion

    #region Progress reporting resilience

    [Test]
    public void NextStep_ProgressThrows_DoesNotPropagate()
    {
        var mockProgress = new Mock<IProgress<ProgressNotificationValue>>();
        mockProgress.Setup(p => p.Report(It.IsAny<ProgressNotificationValue>()))
            .Throws<InvalidOperationException>();

        var reporter = new ProgressReporter(mockProgress.Object, _logger, totalSteps: 1, _mockOutputHelper.Object);

        // Should not throw — progress reporting is best-effort
        Assert.DoesNotThrow(() => reporter.NextStep("test"));
    }

    #endregion
}
