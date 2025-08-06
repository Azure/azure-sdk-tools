using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Azure.Tools.GeneratorAgent.Security;

namespace Azure.Tools.GeneratorAgent
{
    internal class ProcessExecutor
    {
        private readonly ILogger<ProcessExecutor> Logger;

        public ProcessExecutor(ILogger<ProcessExecutor> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            Logger = logger;
        }

        public virtual async Task<Result> ExecuteAsync(
            string command,
            string arguments,
            string? workingDir,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(command);

            if (!SecureProcessConfiguration.IsCommandAllowed(command))
            {
                return Result.Failure($"Command '{command}' is not in the allowed commands list");
            }

            if (!string.IsNullOrEmpty(workingDir))
            {
                Result<string> workingDirValidation = InputValidator.ValidateWorkingDirectory(workingDir);
                if (workingDirValidation.IsFailure)
                {
                    return Result.Failure($"Working directory '{workingDir}' failed security validation: {workingDirValidation.Error}");
                }
            }

            arguments ??= string.Empty;

            try
            {
                using Process process = CreateProcess(command, arguments, workingDir);
                process.Start();

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                bool timedOut = await WaitForProcessWithTimeoutAsync(process, timeout, cancellationToken);
                if (timedOut)
                {
                    return await HandleTimeoutAsync(process, outputTask, errorTask, timeout!.Value, command);
                }

                string output = await outputTask.ConfigureAwait(false);
                string error = await errorTask.ConfigureAwait(false);
                bool success = process.ExitCode == 0;

                LogExecutionResult(success, process.ExitCode, command, error);

                return success 
                    ? Result.Success(output.TrimEnd()) 
                    : Result.Failure($"Process failed with exit code {process.ExitCode}: {error.TrimEnd()}", output.TrimEnd());
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
            {
                return LogAndReturnError("Command not found", command, ex);
            }
            catch (Win32Exception ex)
            {
                return LogAndReturnError("Win32 error starting process", command, ex);
            }
            catch (InvalidOperationException ex)
            {
                return LogAndReturnError("Invalid operation starting process", command, ex);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation("Command execution was cancelled: {Command}", command);
                return Result.Failure("Operation was cancelled");
            }
            catch (Exception ex)
            {
                return LogAndReturnError("Unexpected error executing command", command, ex);
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

        private async Task<Result> HandleTimeoutAsync(
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

            string timeoutError = $"Process timed out after {timeout.TotalMilliseconds}ms";
            string combinedError = string.IsNullOrEmpty(partialError) ? timeoutError : $"{timeoutError}. Partial error output: {partialError}";

            return Result.Failure(combinedError, partialOutput);
        }

        private void LogExecutionResult(bool success, int exitCode, string command, string error)
        {
            if (success)
            {
                Logger.LogDebug("Command succeeded. Command: {Command}", command);
            }
            else
            {
                Logger.LogError("Command failed with exit code {ExitCode}. Command: {Command}. Error: {Error}",
                    exitCode, command, error);
            }
        }

        private Result LogAndReturnError(string errorMessage, string command, Exception ex)
        {
            Logger.LogError(ex, "{ErrorMessage}: {Command}", errorMessage, command);
            return Result.Failure(ex.Message);
        }
    }
}
