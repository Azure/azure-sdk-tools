using System.Diagnostics;
using System.Text;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Go-specific implementation of language repository service.
/// Uses tools like go build, go test, go mod, gofmt, etc. for Go development workflows.
/// </summary>
public class GoLanguageRepoService : LanguageRepoService
{
    public GoLanguageRepoService(string repositoryPath) : base(repositoryPath)
    {
    }

    public override async Task<ICLICheckResponse> AnalyzeDependenciesAsync()
    {
        try
        {
            var (output, exitCode) = await RunCommandAsync(new() { FileName = "go", ArgumentList = { "mod", "tidy" } });
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
            var (output, exitCode) = await RunCommandAsync(new() { FileName = "gofmt", ArgumentList = { "-w" } });
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
            var (output, exitCode) = await RunCommandAsync(new() { FileName = "golangci-lint", ArgumentList = { "./..." } });
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
    private async Task<(string Output, int ExitCode)> RunCommandAsync(ProcessStartInfo psi, CancellationToken ct = null)
    {
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

        return (outputBuilder.ToString(), process.ExitCode);
    }
}
