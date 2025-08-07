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
    private readonly string formatterName = "gofmt";
    private readonly string linterName = "golangci-lint";

    public GoLanguageRepoService(string repositoryPath) : base(repositoryPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            compilerName = "go.exe";
            formatterName = "gofmt.exe";
            linterName = "golangci-lint.exe";
        }
    }

    public override async Task<ICLICheckResponse> AnalyzeDependenciesAsync()
    {
        try
        {
            var (output, exitCode) = await RunCommandAsync(new() { FileName = compilerName, ArgumentList = { "mod", "tidy" } });
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
            var (output, exitCode) = await RunCommandAsync(new() { FileName = formatterName, ArgumentList = { "-w" } });
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
            var (output, exitCode) = await RunCommandAsync(new() { FileName = linterName, ArgumentList = { "./..." } });
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
            var (output, exitCode) = await RunCommandAsync(new() { FileName = "go", ArgumentList = { "test", "-v", "-timeout", "1h", "./..." } });
            return CreateResponse(nameof(RunTestsAsync), exitCode, output);
        }
        catch (Exception ex)
        {
            return CreateFailureResponse($"{nameof(RunTestsAsync)} failed with an exception: {ex}");
        }
    }

    public override async Task<ICLICheckResponse> BuildProjectAsync()
    {
        try
        {
            // does this need to ensure that tests _also_ build, or is it okay to assume that RunTestsAsync() will always be called?
            var (output, exitCode) = await RunCommandAsync(new() { FileName = "go", ArgumentList = { "build" } });
            return CreateResponse(nameof(BuildProjectAsync), exitCode, output);
        }
        catch (Exception ex)
        {
            return CreateFailureResponse($"{nameof(BuildProjectAsync)} failed with an exception: {ex}");
        }
    }

    /// <summary>
    /// Helper method to run command line tools asynchronously.
    /// </summary>
    /// <param name="psi">ProcessStartInfo for the process. NOTE: this parameter is modified.</param>
    /// <param name="prependCommandLine">If true, prepends the command line to the output.</param>
    private static async Task<(string Output, int ExitCode)> RunCommandAsync(ProcessStartInfo psi, bool prependCommandLine = true, CancellationToken ct = default)
    {
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        using var process = new Process()
        {
            StartInfo = psi,
        };

        var outputBuilder = new StringBuilder();

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

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        if (prependCommandLine)
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
                    builder.Append(" \"").Append(arg.Replace("\"", "\\\"")).Append("\"");
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
