using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Factory service for creating language-specific repository services.
/// Detects the language of a repository and returns the appropriate service implementation.
/// </summary>
public class LanguageRepoServiceFactory
{
    /// <summary>
    /// Creates the appropriate language repository service based on the detected language.
    /// </summary>
    /// <param name="repositoryPath">Absolute path to the repository root</param>
    /// <returns>Language-specific repository service</returns>
    public static ILanguageRepoService CreateService(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
            throw new ArgumentException("Repository path cannot be null or empty", nameof(repositoryPath));

        if (!Directory.Exists(repositoryPath))
            throw new DirectoryNotFoundException($"Repository path does not exist: {repositoryPath}");

        var detectedLanguage = DetectLanguage(repositoryPath);

        return detectedLanguage switch
        {
            "python" => new PythonLanguageRepoService(repositoryPath, NullLogger<PythonLanguageRepoService>.Instance),
            "javascript" => new JavaScriptLanguageRepoService(repositoryPath),
            "typescript" => new JavaScriptLanguageRepoService(repositoryPath), // TypeScript uses same tools as JS
            "csharp" => new DotNetLanguageRepoService(repositoryPath),
            "dotnet" => new DotNetLanguageRepoService(repositoryPath),
            "go" => new GoLanguageRepoService(repositoryPath),
            "java" => new JavaLanguageRepoService(repositoryPath),
            _ => new LanguageRepoService(repositoryPath) // Base implementation for unsupported languages
        };
    }

    /// <summary>
    /// Detects the primary language of a repository based on file patterns and configuration files.
    /// First checks for Language-Settings.ps1 as mentioned in the gist, then falls back to file-based detection.
    /// </summary>
    /// <param name="repositoryPath">Path to the repository root</param>
    /// <returns>Detected language string</returns>
    public static string DetectLanguage(string repositoryPath)
    {
        // First, try to detect from eng/scripts/Language-Settings.ps1 as mentioned in the gist
        var languageSettingsPath = Path.Combine(repositoryPath, "eng", "scripts", "Language-Settings.ps1");
        if (File.Exists(languageSettingsPath))
        {
            try
            {
                var content = File.ReadAllText(languageSettingsPath);
                
                // Look for language indicators in the PowerShell file
                if (content.Contains("python", StringComparison.OrdinalIgnoreCase))
                    return "python";
                if (content.Contains("javascript", StringComparison.OrdinalIgnoreCase) || 
                    content.Contains("js", StringComparison.OrdinalIgnoreCase))
                    return "javascript";
                if (content.Contains("typescript", StringComparison.OrdinalIgnoreCase) || 
                    content.Contains("ts", StringComparison.OrdinalIgnoreCase))
                    return "typescript";
                if (content.Contains("csharp", StringComparison.OrdinalIgnoreCase) || 
                    content.Contains("dotnet", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains(".net", StringComparison.OrdinalIgnoreCase))
                    return "csharp";
                if (content.Contains("java", StringComparison.OrdinalIgnoreCase))
                    return "java";
                if (content.Contains("go", StringComparison.OrdinalIgnoreCase))
                    return "go";
            }
            catch
            {
                // Fall through to file-based detection if reading the settings file fails
            }
        }

        // Fall back to file-based detection
        return DetectLanguageFromFiles(repositoryPath);
    }

    /// <summary>
    /// Detects language based on the presence of language-specific files and directories.
    /// </summary>
    /// <param name="repositoryPath">Path to the repository root</param>
    /// <returns>Detected language string</returns>
    private static string DetectLanguageFromFiles(string repositoryPath)
    {
        var files = Directory.GetFiles(repositoryPath, "*", SearchOption.TopDirectoryOnly);
        var directories = Directory.GetDirectories(repositoryPath, "*", SearchOption.TopDirectoryOnly);

        // Check for Python indicators
        if (files.Any(f => Path.GetFileName(f).Equals("setup.py", StringComparison.OrdinalIgnoreCase)) ||
            files.Any(f => Path.GetFileName(f).Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase)) ||
            files.Any(f => Path.GetFileName(f).Equals("requirements.txt", StringComparison.OrdinalIgnoreCase)) ||
            files.Any(f => Path.GetFileName(f).Equals("tox.ini", StringComparison.OrdinalIgnoreCase)))
        {
            return "python";
        }

        // Check for JavaScript/Node.js indicators
        if (files.Any(f => Path.GetFileName(f).Equals("package.json", StringComparison.OrdinalIgnoreCase)) ||
            directories.Any(d => Path.GetFileName(d).Equals("node_modules", StringComparison.OrdinalIgnoreCase)))
        {
            // Check if it's TypeScript by looking for tsconfig.json
            if (files.Any(f => Path.GetFileName(f).Equals("tsconfig.json", StringComparison.OrdinalIgnoreCase)))
            {
                return "typescript";
            }
            return "javascript";
        }

        // Check for .NET/C# indicators
        if (files.Any(f => Path.GetExtension(f).Equals(".sln", StringComparison.OrdinalIgnoreCase)) ||
            files.Any(f => Path.GetExtension(f).Equals(".csproj", StringComparison.OrdinalIgnoreCase)) ||
            files.Any(f => Path.GetFileName(f).Equals("global.json", StringComparison.OrdinalIgnoreCase)) ||
            directories.Any(d => Directory.GetFiles(d, "*.csproj", SearchOption.AllDirectories).Length > 0))
        {
            return "csharp";
        }

        // Check for Java indicators
        if (files.Any(f => Path.GetFileName(f).Equals("pom.xml", StringComparison.OrdinalIgnoreCase)) ||
            files.Any(f => Path.GetFileName(f).Equals("build.gradle", StringComparison.OrdinalIgnoreCase)) ||
            directories.Any(d => Directory.GetFiles(d, "*.java", SearchOption.AllDirectories).Length > 0))
        {
            return "java";
        }

        // Check for Go indicators
        if (files.Any(f => Path.GetFileName(f).Equals("go.mod", StringComparison.OrdinalIgnoreCase)) ||
            files.Any(f => Path.GetFileName(f).Equals("go.sum", StringComparison.OrdinalIgnoreCase)))
        {
            return "go";
        }

        // Default to unknown if no clear indicators are found
        return "unknown";
    }

    /// <summary>
    /// Gets a list of all supported languages.
    /// </summary>
    /// <returns>Array of supported language strings</returns>
    public static string[] GetSupportedLanguages()
    {
        return new[] { "python", "javascript", "typescript", "csharp", "dotnet", "go", "java" };
    }
}
