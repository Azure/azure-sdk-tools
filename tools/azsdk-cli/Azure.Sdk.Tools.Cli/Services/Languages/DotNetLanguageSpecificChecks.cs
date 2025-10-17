using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// .NET-specific implementation of language repository service.
/// Uses tools like dotnet CLI, MSBuild, NuGet, etc. for .NET development workflows.
/// </summary>
public class DotNetLanguageSpecificChecks : ILanguageSpecificChecks
{
    private readonly IProcessHelper _processHelper;
    private readonly INpxHelper _npxHelper;
    private readonly IGitHelper _gitHelper;
    private readonly ILogger<DotNetLanguageSpecificChecks> _logger;

    public DotNetLanguageSpecificChecks(
        IProcessHelper processHelper,
        INpxHelper npxHelper,
        IGitHelper gitHelper,
        ILogger<DotNetLanguageSpecificChecks> logger)
    {
        _processHelper = processHelper;
        _npxHelper = npxHelper;
        _gitHelper = gitHelper;
        _logger = logger;
    }

    /// <summary>
    /// Gets the language-specific path pattern for spelling checks.
    /// </summary>
    /// <param name="packageRepoRoot">Repository root path</param>
    /// <param name="packagePath">Package path</param>
    /// <returns>Path pattern for spelling checks</returns>
    public Task<string> GetSpellingCheckPath(string packageRepoRoot, string packagePath)
    {
        var relativePath = Path.GetRelativePath(packageRepoRoot, packagePath);
        var defaultPath = $"." + Path.DirectorySeparatorChar + relativePath + Path.DirectorySeparatorChar + "api" + Path.DirectorySeparatorChar + "*.cs";
        return Task.FromResult(defaultPath);
    }
        public async Task<CLICheckResponse> ValidateReadmeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        // Implementation for validating README in a Python project
        // Could use markdownlint, etc.
        return await CommonLanguageHelpers.ValidateReadmeCommonAsync(_processHelper, _gitHelper, _logger, packagePath, fixCheckErrors, cancellationToken);
    }

    public async Task<CLICheckResponse> ValidateChangelogAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        // Implementation for validating CHANGELOG in a Python project
        // Could use markdownlint, etc.
        return await CommonLanguageHelpers.ValidateChangelogCommonAsync(this, _processHelper, _gitHelper, _logger, packagePath, fixCheckErrors, cancellationToken);
    }
}
