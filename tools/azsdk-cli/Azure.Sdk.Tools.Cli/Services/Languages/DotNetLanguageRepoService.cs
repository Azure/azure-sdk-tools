using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// .NET/C#-specific implementation of language repository service.
/// Uses tools like dotnet build, dotnet test, dotnet format, etc. for .NET development workflows.
/// </summary>
public class DotNetLanguageRepoService : LanguageRepoService
{
    public DotNetLanguageRepoService(IProcessHelper processHelper, IGitHelper gitHelper) 
        : base(processHelper, gitHelper)
    {
    }

    public override async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct)
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("dotnet", new[] { "restore" }, packagePath);
        return CreateResponseFromProcessResult(result);
    }

    public override async Task<CLICheckResponse> FormatCodeAsync(string packagePath)
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("dotnet", new[] { "format" }, packagePath);
        return CreateResponseFromProcessResult(result);
    }

    public override async Task<CLICheckResponse> LintCodeAsync(string packagePath)
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("dotnet", new[] { "build", "--verbosity", "normal" }, packagePath);
        return CreateResponseFromProcessResult(result);
    }

    public override async Task<CLICheckResponse> RunTestsAsync(string packagePath)
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("dotnet", new[] { "test" }, packagePath);
        return CreateResponseFromProcessResult(result);
    }
}
