// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

/// <summary>
/// Validates by running a command and checking the exit code.
/// </summary>
public class CommandValidator : IValidator
{
    /// <summary>
    /// Gets the name of this validator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the command to run.
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// Gets the arguments for the command.
    /// </summary>
    public string[] Arguments { get; }

    /// <summary>
    /// Gets the working directory relative to the repo root.
    /// If null, uses the repo root.
    /// </summary>
    public string? WorkingDirectory { get; }

    /// <summary>
    /// Gets the expected exit code (default: 0).
    /// </summary>
    public int ExpectedExitCode { get; }

    /// <summary>
    /// Gets the timeout for the command.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Creates a new command validator.
    /// </summary>
    /// <param name="name">Human-readable name for the validator.</param>
    /// <param name="command">The command to run.</param>
    /// <param name="arguments">Command arguments (optional).</param>
    /// <param name="workingDirectory">Working directory relative to repo root (optional).</param>
    /// <param name="expectedExitCode">Expected exit code (default: 0).</param>
    /// <param name="timeout">Command timeout (default: 2 minutes).</param>
    public CommandValidator(
        string name,
        string command,
        string[]? arguments = null,
        string? workingDirectory = null,
        int expectedExitCode = 0,
        TimeSpan? timeout = null)
    {
        Name = name;
        Command = command;
        Arguments = arguments ?? [];
        WorkingDirectory = workingDirectory;
        ExpectedExitCode = expectedExitCode;
        Timeout = timeout ?? TimeSpan.FromMinutes(2);
    }

    public async Task<ValidationResult> ValidateAsync(
        ValidationContext context, 
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var workDir = WorkingDirectory != null
            ? Path.Combine(context.RepoPath, WorkingDirectory)
            : context.RepoPath;

        var startInfo = new ProcessStartInfo
        {
            FileName = Command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workDir
        };

        foreach (var arg in Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);

            var output = await outputTask;
            var error = await errorTask;

            stopwatch.Stop();

            var combinedOutput = string.IsNullOrEmpty(error) 
                ? output 
                : $"{output}\n--- stderr ---\n{error}";

            if (process.ExitCode == ExpectedExitCode)
            {
                return new ValidationResult
                {
                    ValidatorName = Name,
                    Passed = true,
                    Message = $"Command exited with code {process.ExitCode}",
                    Details = combinedOutput,
                    Duration = stopwatch.Elapsed
                };
            }

            return new ValidationResult
            {
                ValidatorName = Name,
                Passed = false,
                Message = $"Expected exit code {ExpectedExitCode}, got {process.ExitCode}",
                Details = combinedOutput,
                Duration = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return ValidationResult.Fail(Name, $"Command timed out after {Timeout}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ValidationResult.Fail(Name, $"Failed to run command: {ex.Message}");
        }
    }
}
