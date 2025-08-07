using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Go-specific implementation of language repository service.
/// Uses tools like go build, go test, go mod, gofmt, etc. for Go development workflows.
/// </summary>
public class GoLanguageRepoService : LanguageRepoService
{
    private readonly string compilerName = "go";
    private readonly string formatterName = "goimports";
    private readonly string linterName = "golangci-lint";

    public GoLanguageRepoService(string packagePath) : base(packagePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            compilerName = "go.exe";
            formatterName = "gofmt.exe";
            linterName = "golangci-lint.exe";
        }
    }

    #region Go specific functions, not part of the LanguageRepoService

    public async Task<ICLICheckResponse> CreateEmptyPackage(string moduleName)
    {
        try
        {
            var (output, exitCode) = await RunCommandAsync(new() { FileName = compilerName, ArgumentList = { "mod", "init", moduleName }, WorkingDirectory = _packagePath });
            return CreateResponse(nameof(CreateEmptyPackage), exitCode, output);
        }
        catch (Exception ex)
        {
            return CreateFailureResponse($"{nameof(CreateEmptyPackage)} failed with an exception\n{ex}");
        }
    }

    #endregion

    public override async Task<ICLICheckResponse> AnalyzeDependenciesAsync(CancellationToken ct = default)
    {
        try
        {
            var (output, exitCode) = await RunCommandsAsync([
                new() { FileName = compilerName, ArgumentList = { "get", "-u", "all" }, WorkingDirectory = _packagePath  },   // update all the dependencies to the latest first
                new() { FileName = compilerName, ArgumentList = { "mod", "tidy" }, WorkingDirectory = _packagePath  }         // now tidy, to cleanup any deps that aren't needed.
            ], false, ct);

            return CreateResponse(nameof(AnalyzeDependenciesAsync), exitCode, output);
        }
        catch (Exception ex)
        {
            return CreateFailureResponse($"{nameof(AnalyzeDependenciesAsync)} failed with an exception\n{ex}");
        }
    }
    public override async Task<ICLICheckResponse> FormatCodeAsync()
    {
        try
        {
            var (output, exitCode) = await RunCommandAsync(new() { FileName = formatterName, ArgumentList = { "-w", "." }, WorkingDirectory = _packagePath });
            return CreateResponse(nameof(FormatCodeAsync), exitCode, output);
        }
        catch (Exception ex)
        {
            return CreateFailureResponse($"{nameof(FormatCodeAsync)} failed with an exception: {ex}");
        }
    }

    public override async Task<ICLICheckResponse> LintCodeAsync()
    {
        try
        {
            var (output, exitCode) = await RunCommandAsync(new() { FileName = linterName, ArgumentList = { "run" }, WorkingDirectory = _packagePath });
            return CreateResponse(nameof(LintCodeAsync), exitCode, output);
        }
        catch (Exception ex)
        {
            return CreateFailureResponse($"{nameof(LintCodeAsync)} failed with an exception: {ex}");
        }
    }

    public override async Task<ICLICheckResponse> RunTestsAsync()
    {
        try
        {
            var (output, exitCode) = await RunCommandAsync(new() { FileName = compilerName, ArgumentList = { "test", "-v", "-timeout", "1h", "./..." }, WorkingDirectory = _packagePath });
            return CreateResponse(nameof(RunTestsAsync), exitCode, output);
        }
        catch (Exception ex)
        {
            return CreateFailureResponse($"{nameof(RunTestsAsync)} failed with an exception: {ex}");
        }
    }

    public async Task<ICLICheckResponse> BuildProjectAsync()
    {
        try
        {
            // does this need to ensure that tests _also_ build, or is it okay to assume that RunTestsAsync() will always be called?
            var (output, exitCode) = await RunCommandAsync(new() { FileName = compilerName, ArgumentList = { "build" }, WorkingDirectory = _packagePath });
            return CreateResponse(nameof(BuildProjectAsync), exitCode, output);
        }
        catch (Exception ex)
        {
            return CreateFailureResponse($"{nameof(BuildProjectAsync)} failed with an exception: {ex}");
        }
    }

    /// <summary>
    /// Runs multiple commands using <see cref="RunCommandAsync"/>. It returns after the first command that returns a non-zero exit code.
    /// </summary>
    /// <param name="psis">A collection of <see cref="ProcessStartInfo"/> objects representing the commands to run.</param>
    /// <param name="echo">If true, echoes all output to the console as well as capturing it to a string.</param>
    /// <param name="ct">Optional <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A tuple containing the combined output of all commands, and the exit code of the last command run.</returns>
    private static async Task<(string Output, int ExitCode)> RunCommandsAsync(IEnumerable<ProcessStartInfo> psis, bool echo = false, CancellationToken ct = default)
    {
        var allOutput = new StringBuilder();
        var lastExitCode = 0;

        var options = new RunCommandOptions() { PrependCommandLine = true, Echo = echo };

        foreach (var psi in psis)
        {
            var (output, exitCode) = await RunCommandAsync(psi, options, ct);
            allOutput.Append(output);

            if (exitCode != 0)
            {
                lastExitCode = exitCode;
                break;
            }
        }

        return (allOutput.ToString(), lastExitCode);
    }


    public class RunCommandOptions
    {
        /// <summary>
        /// Echo all output to the console as well as capturing it to a string
        /// </summary>
        public bool Echo { get; set; }

        /// <summary>
        /// Prepends the command line to the returned output for the command.
        /// </summary>
        public bool PrependCommandLine { get; set; }
    }

    /// <summary>
    /// Helper method to run command line tools asynchronously.
    /// </summary>
    /// <param name="psi">ProcessStartInfo for the process. NOTE: this parameter is modified.</param>
    private static async Task<(string Output, int ExitCode)> RunCommandAsync(ProcessStartInfo psi, RunCommandOptions options = default, CancellationToken ct = default)
    {
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        using var process = new Process()
        {
            StartInfo = psi,
        };

        var outputBuilder = new StringBuilder();

        if (options?.Echo == true)
        {
            process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            Console.WriteLine(e.Data);

                            lock (outputBuilder)
                            {
                                outputBuilder.AppendLine(e.Data);
                            }
                        }
                    };

            process.ErrorDataReceived += (sender, e) =>
            {
                Console.WriteLine(e.Data);

                if (e.Data != null)
                {
                    lock (outputBuilder)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                }
            };
        }
        else
        {
            process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            lock (outputBuilder)
                            {
                                outputBuilder.AppendLine(e.Data);
                            }
                        }
                    };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    lock (outputBuilder)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                }
            };
        }

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        if (options?.PrependCommandLine == true)
        {
            var commandLine = GetApproximateCommandLine(psi);
            outputBuilder.Insert(0, $"Command line: {commandLine}{Environment.NewLine}");
        }

        return (outputBuilder.ToString(), process.ExitCode);
    }

    /// <summary>
    /// Gets an approximation of the command line, given the ProcessStartInfo. It
    /// is mostly best effort, trying to quote command line args, etc...
    /// </summary>
    /// <returns>The command line</returns>
    private static string GetApproximateCommandLine(ProcessStartInfo psi)
    {
        var builder = new StringBuilder();
        builder.Append(psi.FileName);
        builder.Append(' ');

        if (psi.ArgumentList.Count > 0)
        {
            foreach (var arg in psi.ArgumentList)
            {
                // Quote arguments with spaces or special characters
                if (arg.Contains(' ') || arg.Contains('"'))
                {
                    builder.Append(" \"").Append(arg.Replace("\"", "\\\"")).Append('"');
                }
                else
                {
                    builder.Append(' ').Append(arg);
                }
            }
        }
        else
        {
            builder.Append(psi.Arguments);
        }

        return builder.ToString();
    }
}
