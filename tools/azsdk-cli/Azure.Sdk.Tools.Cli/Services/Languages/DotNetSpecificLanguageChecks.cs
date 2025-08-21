using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Go-specific implementation of language repository service.
/// Uses tools like go build, go test, go mod, gofmt, etc. for Go development workflows.
/// </summary>
public class DotNetLanguageSpecificChecks : ILanguageSpecificCheck
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

    public string SupportedLanguage => "Dotnet";

    public bool CanHandle(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !Directory.Exists(packagePath))
        {
            return false;
        }

        var repositoryPath = _gitHelper.DiscoverRepoRoot(packagePath);

        // Get the repository name from the directory path
        var repoName = Path.GetFileName(repositoryPath?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.ToLowerInvariant() ?? "";

        _logger.LogInformation($"Repository name: {repoName}");

        // Extract the language from the repository name
        if (repoName.Contains("azure-sdk-for-dotnet"))
        {
            _logger.LogInformation("Detected language: dotnet from repository name");
            return true;
        }
        return false;
    }
}
