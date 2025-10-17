// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Process helper that checks for a Python venv for running commands that rely on dependencies.
/// </summary>
public sealed class PythonProcessHelper : ProcessHelperBase<PythonProcessHelper>, IPythonProcessHelper
{
    private readonly ILogger<PythonProcessHelper> logger;
    private readonly IRawOutputHelper outputHelper;

    public PythonProcessHelper(ILogger<PythonProcessHelper> logger, IRawOutputHelper outputHelper)
        : base(logger, outputHelper)
    {
        this.logger = logger;
        this.outputHelper = outputHelper;
    }

    public async Task<ProcessResult> RunWithVenv(ProcessOptions options, CancellationToken ct)
    {
        // If there's a venv to activate, we will create and start the process ourselves so we can
        // mutate the ProcessStartInfo.Environment before starting. 

        // Look for an existing venv to use. Prefer VIRTUAL_ENV env var if present.
        var venvPath = Environment.GetEnvironmentVariable("VIRTUAL_ENV") ?? FindVenv(options.WorkingDirectory);

        logger.LogInformation($"PythonProcessHelper using venv at: {venvPath ?? "(none found)"}");

        using var timeoutCts = new CancellationTokenSource(options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = options.Command,
            WorkingDirectory = options.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in options.Args)
        {
            processStartInfo.ArgumentList.Add(arg);
        }

        // If we found a venv, prepend its bin/Scripts to PATH and set VIRTUAL_ENV
        if (!string.IsNullOrWhiteSpace(venvPath))
        {
            try
            {
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                var venvBin = isWindows ? Path.Combine(venvPath, "Scripts") : Path.Combine(venvPath, "bin");
                if (Directory.Exists(venvBin))
                {
                    // Set VIRTUAL_ENV
                    processStartInfo.Environment["VIRTUAL_ENV"] = venvPath;

                    // Prepend to PATH
                    var existingPath = processStartInfo.Environment.ContainsKey("PATH") ? processStartInfo.Environment["PATH"] : Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                    processStartInfo.Environment["PATH"] = venvBin + Path.PathSeparator + existingPath;

                    // Build a shell command that activates the venv and then runs the requested command.
                    // On Unix we use bash -lc "source <venv>/bin/activate && exec <command> <args...>"
                    // On Windows we use cmd.exe /c "call <venv>\Scripts\activate.bat && <command> <args...>"
                    static string QuoteArg(string s)
                    {
                        if (string.IsNullOrEmpty(s))
                        {
                            return "\"\"";
                        }

                        // Simple quoting for arguments that contain spaces or special characters
                        return s.Contains(' ') || s.Contains('\"') || s.Contains('>') || s.Contains('<') || s.Contains('|')
                            ? "\"" + s.Replace("\"", "\\\"") + "\""
                            : s;
                    }

                    // Build the command with arguments quoted as necessary
                    var quotedCmd = QuoteArg(options.Command);
                    var quotedArgs = string.Join(' ', options.Args.Select(QuoteArg));

                    if (!isWindows)
                    {
                        // Use bash to 'source' the activate script (source is a bash builtin)
                        var activatePath = Path.Combine(venvBin, "activate");
                        // Use exec so the launched process replaces the shell and returns proper exit code
                        var composite = $"source {QuoteArg(activatePath)} && exec {quotedCmd} {quotedArgs}".Trim();

                        processStartInfo.FileName = "/bin/bash";
                        processStartInfo.ArgumentList.Clear();
                        processStartInfo.ArgumentList.Add("-lc");
                        processStartInfo.ArgumentList.Add(composite);
                    }
                    else
                    {
                        // Windows: use cmd.exe and call the activate.bat script
                        var activateBat = Path.Combine(venvBin, "activate.bat");
                        var composite = $"call {QuoteArg(activateBat)} && {quotedCmd} {quotedArgs}".Trim();

                        processStartInfo.FileName = "cmd.exe";
                        processStartInfo.ArgumentList.Clear();
                        processStartInfo.ArgumentList.Add("/c");
                        processStartInfo.ArgumentList.Add(composite);
                    }

                }
            }
            catch
            {
                // Don't fail if env modifications can't be applied; fall back to default behavior
            }
        }
        // TODO just trying to make this work, refactor later?
        ProcessResult result = new() { ExitCode = 1 };

        using (var process = new Process())
        {
            process.StartInfo = processStartInfo;
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    lock (result)
                    {
                        result.AppendStdout(e.Data);
                        if (options.LogOutputStream)
                        {
                            outputHelper.OutputConsole($"[{options.ShortName}] {e.Data}");
                        }
                    }
                }
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    lock (result)
                    {
                        result.AppendStderr(e.Data);
                        if (options.LogOutputStream)
                        {
                            outputHelper.OutputConsoleError($"[{options.ShortName}] {e.Data}");
                        }
                    }
                }
            };

            logger.LogInformation(
                "Running command: {command} {args} in {workingDirectory}",
                options.Command, string.Join(" ", options.Args), options.WorkingDirectory);

            process.Start();
            lock (result)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            try
            {
                tryPrintSeparator(options.LogOutputStream);
                await process.WaitForExitAsync(linkedCts.Token);
                tryPrintSeparator(options.LogOutputStream);
            }
            // Insert a more descriptive error message when the task times out
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                logger.LogError("Process '{command}' timed out after {timeout}ms", options.ShortName, options.Timeout.TotalMilliseconds);
                throw new OperationCanceledException($"Process '{options.ShortName}' timed out after {options.Timeout.TotalMilliseconds}ms");
            }

            result.ExitCode = process.ExitCode;
        }

        return result;
    }

    private void tryPrintSeparator(bool logOutputStream)
    {
        try
        {
            var windowWidth = Console.WindowWidth;
            var separatorLength = 80;
            if (windowWidth < 80)
            {
                separatorLength = 10;
            }
            outputHelper.OutputConsole(new string('-', separatorLength));
        }
        catch { }
    }


    private string? FindVenv(string workingDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                workingDirectory = Environment.CurrentDirectory;
            }

            var candidates = new[] { ".venv", "venv", ".env", "env" };

            foreach (var c in candidates)
            {
                var path = Path.Combine(workingDirectory, c);
                if (Directory.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
