// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using ModelContextProtocol;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Provides progress reporting for long-running MCP tool operations using the
/// MCP <c>notifications/progress</c> protocol. Sends periodic heartbeat
/// notifications while a subprocess or long-running task executes, so the
/// caller (client/agent) can see the operation is still alive.
/// </summary>
public static class ProgressReporter
{
    /// <summary>
    /// Default interval between heartbeat progress notifications.
    /// </summary>
    public static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Executes a long-running task while sending periodic MCP progress
    /// notifications to the client. Falls back to logging when progress
    /// reporting is not available (e.g. CLI mode).
    /// </summary>
    /// <typeparam name="T">The return type of the work function.</typeparam>
    /// <param name="progress">
    /// The progress reporter injected by the MCP framework. When running in
    /// MCP mode with a progress token, this is a <c>TokenProgress</c> that
    /// sends <c>notifications/progress</c>. When no token is provided, the
    /// framework supplies a <c>NullProgress</c> (no-op). May be <c>null</c>
    /// in CLI mode.
    /// </param>
    /// <param name="logger">Logger used as a fallback or supplement.</param>
    /// <param name="activityMessage">
    /// A human-readable description of the activity, e.g.
    /// "Running code generation".
    /// </param>
    /// <param name="work">
    /// The long-running work to execute. Receives a <see cref="CancellationToken"/>
    /// that is cancelled when the caller's token is cancelled.
    /// </param>
    /// <param name="ct">Cancellation token for the overall operation.</param>
    /// <param name="heartbeatInterval">
    /// Interval between heartbeat messages. Defaults to
    /// <see cref="DefaultHeartbeatInterval"/>.
    /// </param>
    /// <returns>The result of the <paramref name="work"/> function.</returns>
    public static async Task<T> RunWithProgressAsync<T>(
        IProgress<ProgressNotificationValue>? progress,
        ILogger logger,
        string activityMessage,
        Func<CancellationToken, Task<T>> work,
        CancellationToken ct,
        TimeSpan? heartbeatInterval = null)
    {
        var interval = heartbeatInterval ?? DefaultHeartbeatInterval;

        ReportProgress(progress, logger, 0, activityMessage);

        var workTask = work(ct);
        var elapsed = TimeSpan.Zero;

        while (!workTask.IsCompleted)
        {
            // Wait for either the work to complete or the heartbeat interval to elapse
            try
            {
                await Task.WhenAny(workTask, Task.Delay(interval, ct));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Cancellation requested. Fail fast if work has not completed yet.
                if (!workTask.IsCompleted)
                {
                    throw;
                }

                // If work already completed concurrently, exit the loop and
                // propagate the actual work result/exception below.
                break;
            }

            if (!workTask.IsCompleted)
            {
                elapsed += interval;
                var elapsedSeconds = (float)elapsed.TotalSeconds;
                var message = $"{activityMessage}... ({(int)elapsedSeconds}s elapsed)";
                ReportProgress(progress, logger, elapsedSeconds, message);
            }
        }

        // Propagate any exceptions from the work task
        return await workTask;
    }

    /// <summary>
    /// Reports a single progress update. If an <see cref="IProgress{T}"/> is
    /// available, it delegates to the MCP framework (which sends
    /// <c>notifications/progress</c> when a progress token is present, or
    /// no-ops otherwise). Falls back to logging in CLI mode.
    /// </summary>
    /// <param name="progress">
    /// The progress reporter injected by the MCP framework, or <c>null</c>
    /// in CLI mode (falls back to logging).
    /// </param>
    /// <param name="logger">Logger used as a fallback when <paramref name="progress"/> is <c>null</c>.</param>
    /// <param name="progressValue">
    /// The current progress value (e.g. step number or elapsed seconds).
    /// </param>
    /// <param name="message">A human-readable description of the current progress state.</param>
    /// <param name="total">
    /// The optional total number of steps. When set, clients can display a
    /// progress bar or percentage (e.g. <paramref name="progressValue"/> / <paramref name="total"/>).
    /// Leave <c>null</c> for indeterminate progress such as heartbeat updates.
    /// </param>
    public static void ReportProgress(
        IProgress<ProgressNotificationValue>? progress,
        ILogger logger,
        float progressValue,
        string message,
        float? total = null)
    {
        if (progress is not null)
        {
            try
            {
                progress.Report(new ProgressNotificationValue
                {
                    Progress = progressValue,
                    Total = total,
                    Message = message,
                });
            }
            catch (Exception ex)
            {
                // Progress reporting is best-effort; never fail the tool operation.
                logger.LogDebug(ex, "Failed to send MCP progress notification");
            }
        }
        else
        {
            // Fallback: log the progress message for CLI mode.
            logger.LogInformation("[Progress] {Message}", message);
        }
    }
}
