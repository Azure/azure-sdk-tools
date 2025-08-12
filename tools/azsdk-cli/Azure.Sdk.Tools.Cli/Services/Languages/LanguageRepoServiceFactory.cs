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
    /// Creates the appropriate language repository service based on the detected language.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <param name="processHelper">Process helper for running commands</param>
    /// <param name="gitHelper">Git helper instance for repository operations</param>
    /// <param name="logger">Logger instance for diagnostics</param>
    /// <returns>Language-specific repository service</returns>
    public ILanguageRepoService CreateService(string packagePath, IProcessHelper processHelper, IGitHelper gitHelper, ILogger logger)
    {
        // Use the existing logic but with provided dependencies
        return GetServiceInternal(packagePath, processHelper, gitHelper, logger);
    }

    /// <summary>
    /// Gets the appropriate language repository service based on the detected language.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>Language-specific repository service</returns>
    public ILanguageRepoService CreateService(string packagePath)
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
            _ => _serviceProvider.GetRequiredService<LanguageRepoService>()
        };
    }

    /// <summary>
    /// Detects the primary language of a repository based on the README.md header.
    /// Looks for patterns like "Azure SDK for .NET", "Azure SDK for Python", etc.
    /// </summary>
    /// <param name="repositoryPath">Path to the repository root</param>
    /// <returns>Detected language string</returns>
    public string DetectLanguage(string repositoryPath)
    {        
        // Try to detect from README.md header at repository root
        var readmePath = Path.Combine(repositoryPath, "README.md");
        _logger.LogInformation($"README path: {readmePath}");
        if (!File.Exists(readmePath))
        {
            _logger.LogDebug("README.md not found, language detection unsuccessful");
            return "unknown";
        }

        _logger.LogDebug("Found README.md file at: {ReadmePath}", readmePath);
        try
        {
            var content = File.ReadAllText(readmePath);

            // Look for "Azure SDK for X" pattern in the first line of README
            var firstLine = content.Split('\n').FirstOrDefault()?.Trim().ToLowerInvariant() ?? "";

            // Extract the language from the first line
            if (firstLine.Contains("python"))
            {
                _logger.LogInformation("Detected language: python from README.md header");
                return "python";
            }
            if (firstLine.Contains("javascript"))
            {
                _logger.LogInformation("Detected language: javascript from README.md header");
                return "javascript";
            }
            if (firstLine.Contains(".net"))
            {
                _logger.LogInformation("Detected language: dotnet from README.md header");
                return "dotnet";
            }
            if (firstLine.Contains("java"))
            {
                _logger.LogInformation("Detected language: java from README.md header");
                return "java";
            }
            if (firstLine.Contains("go"))
            {
                _logger.LogInformation("Detected language: go from README.md header");
                return "go";
            }

            _logger.LogWarning("No recognized language found in README.md header");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read README.md file");
            return "unknown";
        }
        return "unknown";
    }
    
}
