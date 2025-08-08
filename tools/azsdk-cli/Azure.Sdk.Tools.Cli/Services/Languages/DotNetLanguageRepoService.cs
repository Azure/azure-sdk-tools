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
    public DotNetLanguageRepoService(string packagePath, IProcessHelper processHelper) 
        : base(packagePath, processHelper)
    {
    }

    public override async Task<CLICheckResponse> AnalyzeDependenciesAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("dotnet", new[] { "restore" }, _packagePath);
        return CreateResponseFromProcessResult(result);
    }

    public override async Task<CLICheckResponse> FormatCodeAsync()
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("dotnet", new[] { "format" }, _packagePath);
        return CreateResponseFromProcessResult(result);
    }

    public override async Task<CLICheckResponse> LintCodeAsync()
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("dotnet", new[] { "build", "--verbosity", "normal" }, _packagePath);
        return CreateResponseFromProcessResult(result);
    }

    public override async Task<CLICheckResponse> RunTestsAsync()
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("dotnet", new[] { "test" }, _packagePath);
        return CreateResponseFromProcessResult(result);
    }
}
