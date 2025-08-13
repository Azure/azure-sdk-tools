using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Factory service for creating language-specific repository services.
/// Detects the language of a repository and returns the appropriate service implementation.
/// </summary>
public class LanguageRepoServiceFactory : ILanguageRepoServiceFactory
{

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LanguageRepoServiceFactory> _logger;
    private readonly IProcessHelper _processHelper;
    private readonly IGitHelper _gitHelper;

    public LanguageRepoServiceFactory(IServiceProvider serviceProvider, ILogger<LanguageRepoServiceFactory> logger, IProcessHelper processHelper, IGitHelper gitHelper)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _processHelper = processHelper;
        _gitHelper = gitHelper;

    }

    /// <summary>
    /// Gets the appropriate language repository service based on the detected language.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="processHelper">Process helper for running commands</param>
    /// <param name="gitHelper">Git helper instance for repository operations</param>
    /// <param name="logger">Logger instance for diagnostics</param>
    /// <returns>Language-specific repository service</returns>
    /// <exception cref="ArgumentException">Thrown when packagePath is null or empty</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when packagePath does not exist</exception>
    /// <exception cref="NotSupportedException">Thrown when the detected language is not supported</exception>
    public ILanguageRepoService GetService(string packagePath, IProcessHelper processHelper, IGitHelper gitHelper, ILogger logger)
    {
        // Use the existing logic but with provided dependencies
        return GetServiceInternal(packagePath, processHelper, gitHelper, logger);
    }

    /// <summary>
    /// Gets the appropriate language repository service based on the detected language.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>Language-specific repository service</returns>
    /// <exception cref="ArgumentException">Thrown when packagePath is null or empty</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when packagePath does not exist</exception>
    /// <exception cref="NotSupportedException">Thrown when the detected language is not supported</exception>
    public ILanguageRepoService GetService(string packagePath)
    {
        return GetServiceInternal(packagePath, _processHelper, _gitHelper, _logger);
    }

    private ILanguageRepoService GetServiceInternal(string packagePath, IProcessHelper processHelper, IGitHelper gitHelper, ILogger logger)
    {
        logger.LogInformation($"Create service for package at: {packagePath}");
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new ArgumentException("Package path cannot be null or empty", nameof(packagePath));
        }

        if (!Directory.Exists(packagePath))
        {
            throw new DirectoryNotFoundException($"Package path does not exist: {packagePath}");
        }

        // Discover the repository root from the project path
        var repoRootPath = gitHelper.DiscoverRepoRoot(packagePath);
        logger.LogInformation($"Discovered repository root: {repoRootPath}");

        // Create language service using factory (detects language automatically)
        logger.LogInformation($"Creating language service for repository at: {repoRootPath}");
        var detectedLanguage = DetectLanguage(repoRootPath);

        return detectedLanguage switch
        {
            "python" => _serviceProvider.GetRequiredService<PythonLanguageRepoService>(),
            "javascript" => _serviceProvider.GetRequiredService<JavaScriptLanguageRepoService>(),
            "dotnet" => _serviceProvider.GetRequiredService<DotNetLanguageRepoService>(),
            "go" => _serviceProvider.GetRequiredService<GoLanguageRepoService>(),
            "java" => _serviceProvider.GetRequiredService<JavaLanguageRepoService>(),
            _ => throw new NotSupportedException($"Language '{detectedLanguage}' is not supported. Supported languages are: python, javascript, dotnet, go, java")
        };
    }

    /// <summary>
    /// Detects the primary language of a repository based on the repository root directory name.
    /// Looks for patterns like "azure-sdk-for-python", "azure-sdk-for-java", etc.
    /// </summary>
    /// <param name="repositoryPath">Path to the repository root</param>
    /// <returns>Detected language string</returns>
    public string DetectLanguage(string repositoryPath)
    {        
        // Get the repository name from the directory path
        var repoName = Path.GetFileName(repositoryPath?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.ToLowerInvariant() ?? "";
        _logger.LogInformation($"Repository name: {repoName}");

        // Extract the language from the repository name
        if (repoName.Contains("azure-sdk-for-python"))
        {
            _logger.LogInformation("Detected language: python from repository name");
            return "python";
        }
        if (repoName.Contains("azure-sdk-for-js"))
        {
            _logger.LogInformation("Detected language: javascript from repository name");
            return "javascript";
        }
        if (repoName.Contains("azure-sdk-for-net"))
        {
            _logger.LogInformation("Detected language: dotnet from repository name");
            return "dotnet";
        }
        if (repoName.Contains("azure-sdk-for-java"))
        {
            _logger.LogInformation("Detected language: java from repository name");
            return "java";
        }
        if (repoName.Contains("azure-sdk-for-go"))
        {
            _logger.LogInformation("Detected language: go from repository name");
            return "go";
        }

        _logger.LogWarning("No recognized language found in repository name: {RepoName}", repoName);
        return "unknown";
    }
    
}
