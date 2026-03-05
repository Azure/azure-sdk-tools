// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using ModelContextProtocol;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class ProgressReporterTests
{
    private TestLogger<ProgressReporterTests> _logger;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<ProgressReporterTests>();
    }

    #region Constructor validation

    [Test]
    public void Constructor_ZeroSteps_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProgressReporter(null, _logger, totalSteps: 0));
    }

    [Test]
    public void Constructor_NegativeSteps_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProgressReporter(null, _logger, totalSteps: -1));
    }

    #endregion

    #region NextStep tests

    [Test]
    public void NextStep_ReportsStepsSequentially()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 3);

        reporter.NextStep("Step one");
        reporter.NextStep("Step two");
        reporter.NextStep("Step three");

        Assert.That(_logger.Logs, Has.Count.EqualTo(3));
        Assert.That(_logger.Logs[0].ToString(), Does.Contain("Step one"));
        Assert.That(_logger.Logs[1].ToString(), Does.Contain("Step two"));
        Assert.That(_logger.Logs[2].ToString(), Does.Contain("Step three"));
    }

    [Test]
    public void NextStep_ExceedsTotalSteps_Throws()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 2);

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

        var reporter = new ProgressReporter(mockProgress.Object, _logger, totalSteps: 3);

        reporter.NextStep("First");
        reporter.NextStep("Second");
        reporter.NextStep("Third");

        Assert.That(reported, Has.Count.EqualTo(3));

        Assert.That(reported[0].Progress, Is.EqualTo(0));
        Assert.That(reported[0].Total, Is.EqualTo(3));
        Assert.That(reported[0].Message, Is.EqualTo("First"));

        Assert.That(reported[1].Progress, Is.EqualTo(1));
        Assert.That(reported[1].Total, Is.EqualTo(3));
        Assert.That(reported[1].Message, Is.EqualTo("Second"));

        Assert.That(reported[2].Progress, Is.EqualTo(2));
        Assert.That(reported[2].Total, Is.EqualTo(3));
        Assert.That(reported[2].Message, Is.EqualTo("Third"));
    }

    [Test]
    public void NextStep_NoProgress_FallsBackToLogger()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 1);

        reporter.NextStep("test message");

        Assert.That(_logger.Logs, Has.Count.EqualTo(1));
        Assert.That(_logger.Logs[0].ToString(), Does.Contain("test message"));
    }

    #endregion

    #region EnsureComplete tests

    [Test]
    public void EnsureComplete_AllStepsReported_DoesNotThrow()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 2);

        reporter.NextStep("Step one");
        reporter.NextStep("Step two");

        Assert.DoesNotThrow(() => reporter.EnsureComplete());
    }

    [Test]
    public void EnsureComplete_MissingSteps_Throws()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 3);

        reporter.NextStep("Step one");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            reporter.EnsureComplete());
        Assert.That(ex!.Message, Does.Contain("Expected 3"));
        Assert.That(ex.Message, Does.Contain("only 1"));
    }

    #endregion

    #region Heartbeat tests

    [Test]
    public async Task StartHeartbeat_EmitsHeartbeatsUntilDisposed()
    {
        var reported = new List<ProgressNotificationValue>();
        var mockProgress = new Mock<IProgress<ProgressNotificationValue>>();
        mockProgress.Setup(p => p.Report(It.IsAny<ProgressNotificationValue>()))
            .Callback<ProgressNotificationValue>(v => reported.Add(v));

        var reporter = new ProgressReporter(mockProgress.Object, _logger, totalSteps: 2);

        reporter.NextStep("Starting work");

        await using (reporter.StartHeartbeat("Working", heartbeatInterval: TimeSpan.FromMilliseconds(50)))
        {
            await Task.Delay(200);
        }

        // Should have initial step report + at least 2 heartbeats
        Assert.That(reported.Count, Is.GreaterThanOrEqualTo(3));

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

        var reporter = new ProgressReporter(mockProgress.Object, _logger, totalSteps: 2);

        reporter.NextStep("Step 1");

        await using (reporter.StartHeartbeat("Working", heartbeatInterval: TimeSpan.FromMilliseconds(50)))
        {
            await Task.Delay(150);
        }

        var countAfterDispose = reported.Count;

        // Wait a bit more — no new heartbeats should appear
        await Task.Delay(200);

        Assert.That(reported.Count, Is.EqualTo(countAfterDispose));
    }

    [Test]
    public async Task StartHeartbeat_NoProgress_FallsBackToLogger()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 2);

        reporter.NextStep("Step 1");

        await using (reporter.StartHeartbeat("Working", heartbeatInterval: TimeSpan.FromMilliseconds(50)))
        {
            await Task.Delay(200);
        }

        // At least the step message + some heartbeats logged
        Assert.That(_logger.Logs.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(_logger.Logs[0].ToString(), Does.Contain("Step 1"));
    }

    [Test]
    public async Task StartHeartbeat_Cancellation_StopsHeartbeat()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 2);
        reporter.NextStep("Step 1");

        using var cts = new CancellationTokenSource();

        var heartbeat = reporter.StartHeartbeat("Working",
            ct: cts.Token,
            heartbeatInterval: TimeSpan.FromMilliseconds(50));

        await Task.Delay(100);
        cts.Cancel();

        // DisposeAsync should complete without throwing
        await heartbeat.DisposeAsync();

        var countAfterCancel = _logger.Logs.Count;
        await Task.Delay(200);

        Assert.That(_logger.Logs.Count, Is.EqualTo(countAfterCancel));
    }

    [Test]
    public async Task StartHeartbeat_FastTask_NoHeartbeats()
    {
        var reported = new List<ProgressNotificationValue>();
        var mockProgress = new Mock<IProgress<ProgressNotificationValue>>();
        mockProgress.Setup(p => p.Report(It.IsAny<ProgressNotificationValue>()))
            .Callback<ProgressNotificationValue>(v => reported.Add(v));

        var reporter = new ProgressReporter(mockProgress.Object, _logger, totalSteps: 2);

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

        var reporter = new ProgressReporter(mockProgress.Object, _logger, totalSteps: 3);

        reporter.NextStep("Step 1"); // step index 0
        reporter.NextStep("Step 2"); // step index 1

        await using (reporter.StartHeartbeat("Working", heartbeatInterval: TimeSpan.FromMilliseconds(50)))
        {
            await Task.Delay(150);
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
        var reporter = new ProgressReporter(null, _logger, totalSteps: 3);
        Assert.That(reporter.CurrentStep, Is.EqualTo(0));

        reporter.NextStep("First");
        Assert.That(reporter.CurrentStep, Is.EqualTo(1));

        reporter.NextStep("Second");
        Assert.That(reporter.CurrentStep, Is.EqualTo(2));
    }

    [Test]
    public void TotalSteps_ReturnsConstructorValue()
    {
        var reporter = new ProgressReporter(null, _logger, totalSteps: 5);
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

        var reporter = new ProgressReporter(mockProgress.Object, _logger, totalSteps: 1);

        // Should not throw — progress reporting is best-effort
        Assert.DoesNotThrow(() => reporter.NextStep("test"));
    }

    #endregion
}
