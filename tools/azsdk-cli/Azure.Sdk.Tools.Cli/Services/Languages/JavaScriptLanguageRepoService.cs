using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// JavaScript/Node.js-specific implementation of language repository service.
/// Uses tools like npm, prettier, eslint, jest, etc. for JavaScript development workflows.
/// </summary>
public class JavaScriptLanguageRepoService : LanguageRepoService
{
    public JavaScriptLanguageRepoService(IProcessHelper processHelper, IGitHelper gitHelper) 
        : base(processHelper, gitHelper)
    {
    }

    public override async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct)
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("npm", new[] { "audit" }, packagePath);
        return CreateResponseFromProcessResult(result);
    }

    public override async Task<CLICheckResponse> FormatCodeAsync(string packagePath)
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("npx", new[] { "prettier", "--write", "." }, packagePath);
        return CreateResponseFromProcessResult(result);
    }

    public override async Task<CLICheckResponse> LintCodeAsync(string packagePath)
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("npx", new[] { "eslint", "." }, packagePath);
        return CreateResponseFromProcessResult(result);
    }

    public override async Task<CLICheckResponse> RunTestsAsync(string packagePath)
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("npm", new[] { "test" }, packagePath);
        return CreateResponseFromProcessResult(result);
    }
}
