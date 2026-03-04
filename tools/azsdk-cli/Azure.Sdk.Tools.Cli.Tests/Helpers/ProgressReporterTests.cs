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

    #region Logging fallback tests (no IProgress — CLI mode)

    [Test]
    public async Task RunWithProgressAsync_FastTask_ReturnsResultWithoutHeartbeat()
    {
        // Arrange — task completes immediately, no heartbeat should fire
        var result = await ProgressReporter.RunWithProgressAsync(
            progress: null,
            _logger,
            "Fast operation",
            _ => Task.FromResult("done"),
            CancellationToken.None,
            heartbeatInterval: TimeSpan.FromSeconds(60));

        // Assert
        Assert.That(result, Is.EqualTo("done"));
        // Only the initial "[Progress] Fast operation..." message should appear
        Assert.That(_logger.Logs, Has.Count.EqualTo(1));
        Assert.That(_logger.Logs[0].ToString(), Does.Contain("Fast operation"));
    }

    [Test]
    public async Task RunWithProgressAsync_SlowTask_EmitsHeartbeats()
    {
        // Arrange — task takes ~350ms, heartbeat every 100ms → expect initial + ~3 heartbeats
        var result = await ProgressReporter.RunWithProgressAsync(
            progress: null,
            _logger,
            "Slow operation",
            async ct =>
            {
                await Task.Delay(350, ct);
                return 42;
            },
            CancellationToken.None,
            heartbeatInterval: TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.That(result, Is.EqualTo(42));
        // At least the initial message + 2 heartbeats (timing may vary slightly)
        Assert.That(_logger.Logs.Count, Is.GreaterThanOrEqualTo(3));

        // First message is the initial progress message (no elapsed)
        Assert.That(_logger.Logs[0].ToString(), Does.Contain("Slow operation"));
        Assert.That(_logger.Logs[0].ToString(), Does.Not.Contain("elapsed"));

        // Subsequent messages include elapsed time
        Assert.That(_logger.Logs[1].ToString(), Does.Contain("elapsed"));
    }

    [Test]
    public async Task RunWithProgressAsync_PropagatesException()
    {
        // Arrange & Act
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await ProgressReporter.RunWithProgressAsync<int>(
                progress: null,
                _logger,
                "Failing operation",
                _ => throw new InvalidOperationException("test error"),
                CancellationToken.None);
        });

        // Assert
        Assert.That(ex!.Message, Is.EqualTo("test error"));
    }

    [Test]
    public async Task RunWithProgressAsync_Cancellation_StopsHeartbeat()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var task = ProgressReporter.RunWithProgressAsync(
            progress: null,
            _logger,
            "Cancellable operation",
            async ct =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return "should not reach";
            },
            cts.Token,
            heartbeatInterval: TimeSpan.FromMilliseconds(50));

        // Let a couple heartbeats fire, then cancel
        await Task.Delay(200);
        cts.Cancel();

        // Assert — should throw OperationCanceledException from the work task
        Assert.ThrowsAsync<TaskCanceledException>(async () => await task);

        // Verify some heartbeat messages were logged before cancellation
        Assert.That(_logger.Logs.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task RunWithProgressAsync_ReturnsComplexType()
    {
        // Arrange & Act — verify it works with tuple return types (like BuildAsync)
        var (success, message) = await ProgressReporter.RunWithProgressAsync(
            progress: null,
            _logger,
            "Complex return",
            _ => Task.FromResult((true, "build succeeded")),
            CancellationToken.None);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(message, Is.EqualTo("build succeeded"));
    }

    [Test]
    public async Task RunWithProgressAsync_DefaultInterval_UsesDefaultHeartbeat()
    {
        // Arrange & Act — just verify it doesn't throw when no interval is specified
        var result = await ProgressReporter.RunWithProgressAsync(
            progress: null,
            _logger,
            "Default interval",
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo("ok"));
        Assert.That(ProgressReporter.DefaultHeartbeatInterval, Is.EqualTo(TimeSpan.FromSeconds(15)));
    }

    #endregion

    #region ReportProgress unit tests

    [Test]
    public void ReportProgress_NoProgress_LogsMessage()
    {
        // Act
        ProgressReporter.ReportProgress(null, _logger, 0, "test message");

        // Assert — should fall back to logging
        Assert.That(_logger.Logs, Has.Count.EqualTo(1));
        Assert.That(_logger.Logs[0].ToString(), Does.Contain("test message"));
    }

    [Test]
    public void ReportProgress_WithProgress_CallsReport()
    {
        // Arrange
        var mockProgress = new Mock<IProgress<ProgressNotificationValue>>();

        // Act
        ProgressReporter.ReportProgress(mockProgress.Object, _logger, 15, "heartbeat message");

        // Assert — should call Report on the IProgress, not log
        mockProgress.Verify(p => p.Report(It.Is<ProgressNotificationValue>(v =>
            v.Progress == 15 && v.Total == null && v.Message == "heartbeat message")), Times.Once);
        Assert.That(_logger.Logs, Has.Count.EqualTo(0));
    }

    [Test]
    public void ReportProgress_WithTotal_SetsTotal()
    {
        // Arrange
        var mockProgress = new Mock<IProgress<ProgressNotificationValue>>();

        // Act
        ProgressReporter.ReportProgress(mockProgress.Object, _logger, 2, "step 2 of 4", total: 4);

        // Assert — Total should be set on the notification value
        mockProgress.Verify(p => p.Report(It.Is<ProgressNotificationValue>(v =>
            v.Progress == 2 && v.Total == 4 && v.Message == "step 2 of 4")), Times.Once);
    }

    [Test]
    public async Task RunWithProgressAsync_WithProgress_ReportsViaIProgress()
    {
        // Arrange — use a real IProgress to capture reported values
        var reported = new List<ProgressNotificationValue>();
        var progress = new Progress<ProgressNotificationValue>(v => reported.Add(v));

        // Act — slow task with heartbeats
        var result = await ProgressReporter.RunWithProgressAsync(
            progress,
            _logger,
            "MCP operation",
            async ct =>
            {
                await Task.Delay(250, ct);
                return "ok";
            },
            CancellationToken.None,
            heartbeatInterval: TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.That(result, Is.EqualTo("ok"));
        // Give a moment for Progress<T> callbacks (they run on SynchronizationContext)
        await Task.Delay(50);
        Assert.That(reported.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(reported[0].Message, Does.Contain("MCP operation"));
        // When IProgress is provided, we should NOT fall back to logger
        Assert.That(_logger.Logs, Has.Count.EqualTo(0));
    }

    #endregion
}
