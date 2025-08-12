using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Interface for creating language-specific repository services.
/// </summary>
public interface ILanguageRepoServiceFactory
{
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
    ILanguageRepoService GetService(string packagePath, IProcessHelper processHelper, IGitHelper gitHelper, ILogger logger);

    /// <summary>
    /// Gets the appropriate language repository service based on the detected language.
    /// </summary>
    /// <param name="packagePath">Absolute path to the package directory</param>
    /// <returns>Language-specific repository service</returns>
    /// <exception cref="ArgumentException">Thrown when packagePath is null or empty</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when packagePath does not exist</exception>
    /// <exception cref="NotSupportedException">Thrown when the detected language is not supported</exception>
    ILanguageRepoService GetService(string packagePath);

    /// <summary>
    /// Detects the primary language of a repository based on the README.md header.
    /// </summary>
    /// <param name="repositoryPath">Path to the repository root</param>
    /// <returns>Detected language string</returns>
    string DetectLanguage(string repositoryPath);

}
