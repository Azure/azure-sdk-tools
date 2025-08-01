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

        public virtual async Task<(bool Success, string Output, string Error)> ExecuteAsync(
            string command,
            string arguments,
            string? workingDir,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                Logger.LogError("Security violation: Attempted to execute null or empty command");
                throw new ArgumentException("Command cannot be null or empty", nameof(command));
            }

            if (!SecureProcessConfiguration.IsCommandAllowed(command))
            {
                Logger.LogError("Security violation: Attempted to execute unauthorized command: {Command}", command);
                throw new UnauthorizedAccessException($"Command '{command}' is not in the allowed commands list");
            }

            if (!string.IsNullOrEmpty(workingDir))
            {
                ValidationResult workingDirValidation = InputValidator.ValidateWorkingDirectory(workingDir);
                if (!workingDirValidation.IsValid)
                {
                    Logger.LogError("Security violation: Invalid working directory: {WorkingDirectory} - {Error}", 
                        workingDir, workingDirValidation.ErrorMessage);
                    throw new UnauthorizedAccessException($"Working directory '{workingDir}' failed security validation: {workingDirValidation.ErrorMessage}");
                }
            }

            arguments ??= string.Empty;

            Logger.LogInformation("Executing validated command: {Command} with args: {Arguments} in directory: {WorkingDirectory}", 
                command, 
                arguments.Length > 100 ? arguments.Substring(0, 100) + "..." : arguments,
                workingDir ?? Environment.CurrentDirectory);

            try
            {
                using Process process = CreateProcess(command, arguments, workingDir);

                process.Start();

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                if (timeout.HasValue)
                {
                    using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(timeout.Value);

                    try
                    {
                        await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        Logger.LogError("Process timed out after {Timeout}ms: {Command}", timeout.Value.TotalMilliseconds, command);

                        try
                        {
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            {
                                process.Kill(entireProcessTree: true);
                            }
                            else
                            {
                                process.Kill();
                            }
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

                        string timeoutError = $"Process timed out after {timeout.Value.TotalMilliseconds}ms";
                        string combinedError = string.IsNullOrEmpty(partialError) ? timeoutError : $"{timeoutError}. Partial error output: {partialError}";
                        
                        return (false, partialOutput.TrimEnd(), combinedError);
                    }
                }
                else
                {
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }

                string output = await outputTask.ConfigureAwait(false);
                string error = await errorTask.ConfigureAwait(false);

                bool success = process.ExitCode == 0;
                
                if (!success)
                {
                    Logger.LogError("Command failed with exit code {ExitCode}. Command: {Command}. Error: {Error}",
                        process.ExitCode, command, error);
                }
                else
                {
                    Logger.LogDebug("Command succeeded. Command: {Command}", command);
                }

                return (success, output.TrimEnd(), error.TrimEnd());
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
            {
                Logger.LogError("Command not found: {Command}", command);
                return (false, string.Empty, "Command not found");
            }
            catch (Win32Exception ex)
            {
                Logger.LogError(ex, "Win32 error starting process: {Command}", command);
                return (false, string.Empty, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogError(ex, "Invalid operation starting process: {Command}", command);
                return (false, string.Empty, ex.Message);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation("Command execution was cancelled: {Command}", command);
                return (false, string.Empty, "Operation was cancelled");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error executing command: {Command}", command);
                return (false, string.Empty, ex.Message);
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
    }
}
