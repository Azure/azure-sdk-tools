using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Go-specific implementation of language repository service.
/// Uses tools like go build, go test, go mod, gofmt, etc. for Go development workflows.
/// </summary>
public class GoLanguageRepoService : LanguageRepoService
{
    private readonly ILogger<GoLanguageRepoService> _logger;
    private readonly string compilerName = "go";
    private readonly string formatterName = "goimports";
    private readonly string linterName = "golangci-lint";

    public GoLanguageRepoService(IProcessHelper processHelper, IGitHelper gitHelper, ILogger<GoLanguageRepoService> logger) 
        : base(processHelper, gitHelper)
    {
        _logger = logger;
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            compilerName = "go.exe";
            formatterName = "gofmt.exe";
            linterName = "golangci-lint.exe";
        }
    }

    #region Go specific functions, not part of the LanguageRepoService

    public async Task<CLICheckResponse> CreateEmptyPackage(string packagePath, string moduleName)
    {
        try
        {
            await Task.CompletedTask;
            var result = _processHelper.RunProcess(compilerName, new[] { "mod", "init", moduleName }, packagePath);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(CreateEmptyPackage));
            return new FailureCLICheckResponse(1, $"{nameof(CreateEmptyPackage)} failed with an exception", ex.Message);
        }
    }

    #endregion

    public override async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct = default)
    {
        try
        {
            await Task.CompletedTask;
            // Update all dependencies to the latest first
            var updateResult = _processHelper.RunProcess(compilerName, new[] { "get", "-u", "all" }, packagePath);
            if (updateResult.ExitCode != 0)
            {
                return CreateResponseFromProcessResult(updateResult);
            }
            
            // Now tidy, to cleanup any deps that aren't needed
            var tidyResult = _processHelper.RunProcess(compilerName, new[] { "mod", "tidy" }, packagePath);
            return CreateResponseFromProcessResult(tidyResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(AnalyzeDependenciesAsync));
            return new FailureCLICheckResponse(1, $"{nameof(AnalyzeDependenciesAsync)} failed with an exception", ex.Message);
        }
    }
    public override async Task<CLICheckResponse> FormatCodeAsync(string packagePath)
    {
        try
        {
            await Task.CompletedTask;
            var result = _processHelper.RunProcess(formatterName, new[] { "-w", "." }, packagePath);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(FormatCodeAsync));
            return new FailureCLICheckResponse(1, $"{nameof(FormatCodeAsync)} failed with an exception", ex.Message);
        }
    }

    public override async Task<CLICheckResponse> LintCodeAsync(string packagePath)
    {
        try
        {
            await Task.CompletedTask;
            var result = _processHelper.RunProcess(linterName, new[] { "run" }, packagePath);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(LintCodeAsync));
            return new FailureCLICheckResponse(1, $"{nameof(LintCodeAsync)} failed with an exception", ex.Message);
        }
    }

    public override async Task<CLICheckResponse> RunTestsAsync(string packagePath)
    {
        try
        {
            await Task.CompletedTask;
            var result = _processHelper.RunProcess(compilerName, new[] { "test", "-v", "-timeout", "1h", "./..." }, packagePath);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(RunTestsAsync));
            return new FailureCLICheckResponse(1, $"{nameof(RunTestsAsync)} failed with an exception", ex.Message);
        }
    }

    public async Task<CLICheckResponse> BuildProjectAsync(string packagePath)
    {
        try
        {
            await Task.CompletedTask;
            var result = _processHelper.RunProcess(compilerName, new[] { "build" }, packagePath);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(BuildProjectAsync));
            return new FailureCLICheckResponse(1, $"{nameof(BuildProjectAsync)} failed with an exception", ex.Message);
        }
    }

}
