using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Azure.Tools.GeneratorAgent.Exceptions;
using Azure.Tools.GeneratorAgent.Security;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    internal class ProcessExecutionService
    {
        private readonly ILogger<ProcessExecutionService> Logger;

        public ProcessExecutionService(ILogger<ProcessExecutionService> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            Logger = logger;
        }

        public virtual async Task<Result<object>> ExecuteAsync(
            string command,
            string arguments,
            string? workingDir,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(command);

            if (!SecureProcessConfiguration.IsCommandAllowed(command))
            {
                throw new UnauthorizedAccessException($"Command '{command}' is not in the allowed commands list");
            }

            if (!string.IsNullOrEmpty(workingDir))
            {
                workingDir = InputValidator.ValidateWorkingDirectory(workingDir);
            }

            arguments ??= string.Empty;

            try
            {
                using Process process = CreateProcess(command, arguments, workingDir);
                process.Start();

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                bool timedOut = await WaitForProcessWithTimeoutAsync(process, timeout, cancellationToken).ConfigureAwait(false);
                if (timedOut)
                {
                    return await HandleTimeoutAsync(process, outputTask, errorTask, timeout!.Value, command).ConfigureAwait(false);
                }

                string output = await outputTask.ConfigureAwait(false);
                string error = await errorTask.ConfigureAwait(false);
                bool success = process.ExitCode == 0;

                LogExecutionResult(success, process.ExitCode, command, error, output);

                if (!success)
                {
                    return Result<object>.Failure(
                        new GeneralProcessExecutionException($"Process failed with exit code {process.ExitCode}", 
                            command, 
                            output, 
                            error,
                            process.ExitCode));
                }

                return Result<object>.Success(output.TrimEnd());
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
            {
                Logger.LogError(ex, "Command not found: {Command}", command);
                return Result<object>.Failure(ex);
            }
            catch (Win32Exception ex)
            {
                Logger.LogError(ex, "Win32 error starting process: {Command}", command);
                return Result<object>.Failure(ex);
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogError(ex, "Invalid operation starting process: {Command}", command);
                return Result<object>.Failure(ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogError(ex, "Unexpected error executing command: {Command}", command);
                return Result<object>.Failure(ex);
            }
        }

        protected virtual Process CreateProcess(string command, string arguments, string? workingDirectory)
        {
            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
        }

        private async Task<bool> WaitForProcessWithTimeoutAsync(Process process, TimeSpan? timeout, CancellationToken cancellationToken)
        {
            if (!timeout.HasValue)
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout.Value);

            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                return false;
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return true;
            }
        }

        private async Task<Result<object>> HandleTimeoutAsync(
            Process process,
            Task<string> outputTask,
            Task<string> errorTask,
            TimeSpan timeout,
            string command)
        {
            Logger.LogError("Process timed out after {Timeout}ms: {Command}", timeout.TotalMilliseconds, command);

            try
            {
                process.Kill(entireProcessTree: RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            }
            catch (Exception killEx)
            {
                Logger.LogWarning(killEx, "Failed to kill timed-out process");
            }

            string partialOutput = string.Empty;
            string partialError = string.Empty;

            try
            {
                if (outputTask.IsCompleted)
                {
                    partialOutput = await outputTask.ConfigureAwait(false);
                }
                if (errorTask.IsCompleted)
                {
                    partialError = await errorTask.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to capture partial output from timed-out process");
            }

            string timeoutMessage = $"Process timed out after {timeout.TotalMilliseconds}ms";
            if (!string.IsNullOrEmpty(partialError))
            {
                timeoutMessage += $". Partial error output: {partialError}";
            }

            return Result<object>.Failure(new TimeoutException(timeoutMessage));
        }

        private void LogExecutionResult(bool success, int exitCode, string command, string error, string output = "")
        {
            if (success)
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Command {Command} succeeded", command);
                }
            }
            else
            {
                Logger.LogError("Command {Command} failed with exit code {ExitCode}. Error output: {ErrorOutput}. Error: {Error}",
                    command, exitCode, output, error);
            }
        }
    }
}
