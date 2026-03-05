// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using ModelContextProtocol;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Provides stepped progress reporting for MCP tool operations using the
/// MCP <c>notifications/progress</c> protocol. Automatically tracks the
/// current step index and total, so callers only need to declare the total
/// once and call <see cref="NextStep"/> for each phase.
/// <para>
/// For long-running phases, call <see cref="StartHeartbeat"/> to receive an
/// <see cref="IAsyncDisposable"/> scope that sends periodic "still alive"
/// notifications until the scope is disposed via <c>await using</c>.
/// </para>
/// </summary>
public class ProgressReporter
{
    private readonly IProgress<ProgressNotificationValue>? _progress;
    private readonly ILogger _logger;
    private readonly int _totalSteps;
    private int _currentStep;

    /// <summary>
    /// Default interval between heartbeat progress notifications.
    /// </summary>
    public static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(15);

    /// <summary>Number of steps completed so far (0 before any step, equals <see cref="TotalSteps"/> when all steps are done).</summary>
    public int CurrentStep => _currentStep;

    /// <summary>Total number of declared steps.</summary>
    public int TotalSteps => _totalSteps;

    /// <summary>
    /// Creates a new <see cref="ProgressReporter"/> with a fixed number of steps.
    /// </summary>
    /// <param name="progress">
    /// The MCP progress reporter, or <c>null</c> in CLI mode (falls back to logging).
    /// </param>
    /// <param name="logger">Logger used as a fallback when <paramref name="progress"/> is <c>null</c>.</param>
    /// <param name="totalSteps">The total number of steps that will be reported.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="totalSteps"/> is less than 1.</exception>
    public ProgressReporter(IProgress<ProgressNotificationValue>? progress, ILogger logger, int totalSteps)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(totalSteps, 1);
        _progress = progress;
        _logger = logger;
        _totalSteps = totalSteps;
    }

    /// <summary>
    /// Advances to the next step and reports it. The step index is
    /// auto-incremented; callers do not need to track it manually.
    /// </summary>
    /// <param name="message">A human-readable description of this step.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when more steps are reported than the declared <see cref="TotalSteps"/>.
    /// </exception>
    public void NextStep(string message)
    {
        if (_currentStep >= _totalSteps)
        {
            throw new InvalidOperationException(
                $"Reported more steps ({_currentStep + 1}) than declared total ({_totalSteps}).");
        }

        _currentStep++;
        ReportProgress(_currentStep, message, _totalSteps);
    }

    /// <summary>
    /// Starts a heartbeat scope that sends periodic progress notifications for
    /// the current step. The heartbeat runs in the background until the returned
    /// <see cref="IAsyncDisposable"/> is disposed via <c>await using</c>.
    /// <para>
    /// This does <b>not</b> advance the step counter. Call <see cref="NextStep"/>
    /// before <see cref="StartHeartbeat"/> to advance to the long-running step,
    /// then dispose the heartbeat scope when the work completes.
    /// </para>
    /// </summary>
    /// <param name="message">
    /// A human-readable description of the long-running activity.
    /// </param>
    /// <param name="ct">Cancellation token for the overall operation.</param>
    /// <param name="heartbeatInterval">
    /// Interval between heartbeat messages. Defaults to
    /// <see cref="DefaultHeartbeatInterval"/>.
    /// </param>
    /// <returns>
    /// An <see cref="IAsyncDisposable"/> that stops the heartbeat when disposed.
    /// Use with <c>await using</c> to ensure cleanup.
    /// </returns>
    public IAsyncDisposable StartHeartbeat(string message, CancellationToken ct = default, TimeSpan? heartbeatInterval = null)
    {
        var interval = heartbeatInterval ?? DefaultHeartbeatInterval;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var stepAtStart = _currentStep;

        // Launch the heartbeat loop in the background
        var loopTask = RunHeartbeatLoopAsync(message, stepAtStart, interval, cts.Token);

        return new HeartbeatScope(loopTask, cts);
    }

    /// <summary>
    /// Validates that all declared steps were reported. Call at the end of an
    /// operation to catch missing steps during testing or development.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when fewer steps were reported than the declared <see cref="TotalSteps"/>.
    /// </exception>
    public void EnsureComplete()
    {
        if (_currentStep != _totalSteps)
        {
            throw new InvalidOperationException(
                $"Expected {_totalSteps} progress steps but only {_currentStep} were reported.");
        }
    }

    private async Task RunHeartbeatLoopAsync(string message, int step, TimeSpan interval, CancellationToken ct)
    {
        var elapsed = TimeSpan.Zero;

        try
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(ct))
            {
                elapsed += interval;
                var heartbeatMessage = $"{message}... ({(int)elapsed.TotalSeconds}s elapsed)";
                ReportProgress(step, heartbeatMessage, _totalSteps);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the heartbeat scope is disposed or outer ct is cancelled.
        }
        catch (Exception ex)
        {
            // Heartbeat is best-effort; log but never propagate.
            _logger.LogDebug(ex, "Heartbeat loop terminated unexpectedly");
        }
    }

    private void ReportProgress(float progressValue, string message, float? total)
    {
        if (_progress is not null)
        {
            try
            {
                _progress.Report(new ProgressNotificationValue
                {
                    Progress = progressValue,
                    Total = total,
                    Message = message,
                });
            }
            catch (Exception ex)
            {
                // Progress reporting is best-effort; never fail the tool operation.
                _logger.LogDebug(ex, "Failed to send MCP progress notification");
            }
        }
        else
        {
            // Fallback: render an ASCII progress bar for CLI mode.
            var bar = FormatProgressBar(progressValue, total);
            _logger.LogInformation("{Bar} {Message}", bar, message);
        }
    }

    /// <summary>
    /// Formats an ASCII progress bar string, e.g. <c>[████████░░░░░░░░░░░░] 50%</c>.
    /// </summary>
    private static string FormatProgressBar(float progressValue, float? total, int barWidth = 20)
    {
        if (total is null or <= 0)
        {
            return $"[{"░".PadRight(barWidth, '░')}]  0%";
        }

        var fraction = Math.Clamp(progressValue / total.Value, 0f, 1f);
        var filled = (int)Math.Round(fraction * barWidth);
        var empty = barWidth - filled;
        var percentage = (int)Math.Round(fraction * 100);

        return $"[{new string('█', filled)}{new string('░', empty)}] {percentage,3}%";
    }

    /// <summary>
    /// An <see cref="IAsyncDisposable"/> scope that stops the heartbeat loop
    /// and waits for it to exit before returning.
    /// </summary>
    private sealed class HeartbeatScope(Task loopTask, CancellationTokenSource cts) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await cts.CancelAsync();
            try
            {
                await loopTask;
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
            cts.Dispose();
        }
    }
}
