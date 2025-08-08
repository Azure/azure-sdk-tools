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
    public JavaScriptLanguageRepoService(string packagePath, IProcessHelper processHelper) 
        : base(packagePath, processHelper)
    {
    }

    public override async Task<CLICheckResponse> AnalyzeDependenciesAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("npm", new[] { "audit" }, _packagePath);
        return CreateResponseFromProcessResult(result);
    }

    public override async Task<CLICheckResponse> FormatCodeAsync()
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("npx", new[] { "prettier", "--write", "." }, _packagePath);
        return CreateResponseFromProcessResult(result);
    }

    public override async Task<CLICheckResponse> LintCodeAsync()
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("npx", new[] { "eslint", "." }, _packagePath);
        return CreateResponseFromProcessResult(result);
    }

    public override async Task<CLICheckResponse> RunTestsAsync()
    {
        await Task.CompletedTask;
        var result = _processHelper.RunProcess("npm", new[] { "test" }, _packagePath);
        return CreateResponseFromProcessResult(result);
    }
}
