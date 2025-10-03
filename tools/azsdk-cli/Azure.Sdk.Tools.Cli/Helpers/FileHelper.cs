// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    /// <summary>
    /// Helper class for file and directory operations.
    /// </summary>
    public static class FileHelper
    {
        /// <summary>
        /// Validates that the specified directory exists and is empty.
        /// </summary>
        /// <param name="dir">The directory path to validate.</param>
        /// <returns>An error message if validation fails, or null if validation passes.</returns>
        public static string? ValidateEmptyDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                return "Directory must be defined and not only whitespace";
            }

            var fullDir = Path.GetFullPath(dir.Trim());
            if (string.IsNullOrEmpty(fullDir))
            {
                return $"Directory '{dir}' could not be resolved to a full path.";
            }

            if (!Directory.Exists(fullDir))
            {
                return $"Directory '{fullDir}' does not exist.";
            }

            if (Directory.GetFileSystemEntries(fullDir).Length != 0)
            {
                return $"Directory '{fullDir}' points to a non-empty directory.";
            }

            return null; // Validation passed
        }

        /// <summary>
        /// Represents information and operations for an Azure SDK package directory.
        /// </summary>
        public class PackageInfo
        {
            private readonly string packagePath;
            private readonly string repoRoot;
            private readonly string relativePath;
            private readonly ILanguageSpecificCheckResolver? languageResolver;
            private string? cachedLanguage;

            /// <summary>
            /// Initializes a new instance of the PackageInfo class.
            /// </summary>
            /// <param name="packagePath">Path to an Azure SDK package directory</param>
            /// <param name="languageResolver">Optional language resolver for accurate language detection</param>
            /// <exception cref="ArgumentException">Thrown when the path is not under an Azure SDK repository structure</exception>
            public PackageInfo(string packagePath, ILanguageSpecificCheckResolver? languageResolver = null)
            {
                this.packagePath = Path.GetFullPath(packagePath);
                this.languageResolver = languageResolver;

                var sdkSeparator = $"{Path.DirectorySeparatorChar}sdk{Path.DirectorySeparatorChar}";
                var pieces = this.packagePath.Split(sdkSeparator);

                if (pieces.Length != 2)
                {
                    throw new ArgumentException(
                        $"Path '{packagePath}' is not under an Azure SDK repository with 'sdk' subfolder. " +
                        "Expected structure: /path/to/azure-sdk-for-<language>/sdk/<service>/<package>",
                        nameof(packagePath));
                }

                repoRoot = pieces[0];
                relativePath = pieces[1];
            }

            /// <summary>
            /// Gets the repository root path.
            /// </summary>
            public string RepoRoot => repoRoot;

            /// <summary>
            /// Gets the relative path under 'sdk/'.
            /// </summary>
            public string RelativePath => relativePath;

            /// <summary>
            /// Gets the full package path.
            /// </summary>
            public string PackagePath => packagePath;

            /// <summary>
            /// Gets the package name (last directory component).
            /// </summary>
            public string PackageName => Path.GetFileName(packagePath);

            /// <summary>
            /// Gets the service name (parent directory of package).
            /// </summary>
            public string ServiceName => Path.GetFileName(Path.GetDirectoryName(packagePath)) ?? string.Empty;

            /// <summary>
            /// Gets the programming language, first trying the LanguageSpecificCheckResolver if available,
            /// then falling back to file-based detection, then repository name patterns.
            /// </summary>
            public string Language
            {
                get
                {
                    if (cachedLanguage != null)
                    {
                        return cachedLanguage;
                    }

                    // Try to get language from the resolver first (most accurate)
                    if (languageResolver != null)
                    {
                        var languageCheck = languageResolver.GetLanguageCheckAsync(packagePath).GetAwaiter().GetResult();
                        if (languageCheck?.SupportedLanguage != null)
                        {
                            // Normalize the language (convert JavaScript to TypeScript, etc.)
                            cachedLanguage = NormalizeLanguage(languageCheck.SupportedLanguage);
                            return cachedLanguage;
                        }
                    }
                    throw new InvalidOperationException(
                    $"Unable to detect programming language for package at '{packagePath}'. ");
                }
            }

            /// <summary>
            /// Gets the default samples directory path for this package's language.
            /// </summary>
            public string GetSamplesDirectory()
            {
                return GetLanguageDefaultSamplesDir(Language, packagePath);
            }

            /// <summary>
            /// Gets the appropriate file extension for this package's language.
            /// </summary>
            public string GetFileExtension()
            {
                return GetLanguageFileExtension(Language);
            }

            /// <summary>
            /// Gets the package version by looking for common version files or directory patterns.
            /// </summary>
            public string? GetPackageVersion()
            {
                // Try common version file patterns
                var versionFiles = new[]
                {
                    Path.Combine(packagePath, "package.json"),          // JavaScript/TypeScript
                    Path.Combine(packagePath, "pom.xml"),               // Java
                    Path.Combine(packagePath, "setup.py"),              // Python
                    Path.Combine(packagePath, "pyproject.toml"),        // Python (newer)
                    Path.Combine(packagePath, "go.mod"),                // Go
                    Path.Combine(packagePath, "*.csproj")               // .NET (wildcard)
                };

                // For .NET, find any .csproj file
                if (Language == "dotnet")
                {
                    var csprojFiles = Directory.GetFiles(packagePath, "*.csproj");
                    if (csprojFiles.Length > 0)
                    {
                        return TryExtractVersionFromCsProj(csprojFiles[0]);
                    }
                }

                foreach (var versionFile in versionFiles.Where(f => !f.Contains("*")))
                {
                    if (File.Exists(versionFile))
                    {
                        return TryExtractVersionFromFile(versionFile);
                    }
                }

                return null;
            }

            private string? TryExtractVersionFromFile(string filePath)
            {
                try
                {
                    var content = File.ReadAllText(filePath);
                    var fileName = Path.GetFileName(filePath);

                    return fileName switch
                    {
                        "package.json" => ExtractFromJson(content, "version"),
                        "setup.py" => ExtractFromPython(content),
                        "pyproject.toml" => ExtractFromToml(content),
                        "go.mod" => ExtractFromGoMod(content),
                        _ => null
                    };
                }
                catch
                {
                    return null;
                }
            }

            private string? TryExtractVersionFromCsProj(string csprojPath)
            {
                try
                {
                    var content = File.ReadAllText(csprojPath);
                    var versionMatch = System.Text.RegularExpressions.Regex.Match(content, @"<Version>([^<]+)</Version>");
                    return versionMatch.Success ? versionMatch.Groups[1].Value : null;
                }
                catch
                {
                    return null;
                }
            }

            private string? ExtractFromJson(string content, string key)
            {
                var match = System.Text.RegularExpressions.Regex.Match(content, $@"""{key}""\s*:\s*""([^""]+)""");
                return match.Success ? match.Groups[1].Value : null;
            }

            private string? ExtractFromPython(string content)
            {
                var match = System.Text.RegularExpressions.Regex.Match(content, @"version\s*=\s*[""']([^""']+)[""']");
                return match.Success ? match.Groups[1].Value : null;
            }

            private string? ExtractFromToml(string content)
            {
                var match = System.Text.RegularExpressions.Regex.Match(content, @"version\s*=\s*[""']([^""']+)[""']");
                return match.Success ? match.Groups[1].Value : null;
            }

            private string? ExtractFromGoMod(string content)
            {
                // Go modules don't typically have version in go.mod, return null
                return null;
            }
        }

        /// <summary>
        /// Gets the default output directory for samples based on the target language and package path.
        /// </summary>
        /// <param name="language">The target programming language</param>
        /// <param name="packagePath">Path to the package directory</param>
        /// <returns>The appropriate samples directory path for the language</returns>
        public static string GetLanguageDefaultSamplesDir(string language, string packagePath)
        {
            return language.ToLowerInvariant() switch
            {
                "dotnet" => Path.Combine(packagePath, "tests", "samples"),
                "java" => Path.Combine(packagePath, "src", "samples", "java"),
                "typescript" => Path.Combine(packagePath, "samples-dev"),
                "python" => Path.Combine(packagePath, "samples"),
                "go" => Path.Combine(packagePath, "examples"),
                _ => throw new ArgumentException($"Unsupported language: '{language}'. Supported languages are: dotnet, java, typescript, python, go", nameof(language))
            };
        }

        /// <summary>
        /// Gets the appropriate file extension for the target language.
        /// </summary>
        /// <param name="language">The target programming language</param>
        /// <returns>The file extension including the dot (e.g., ".ts", ".py")</returns>
        public static string GetLanguageFileExtension(string language)
        {
            return language.ToLowerInvariant() switch
            {
                "dotnet" => ".cs",
                "java" => ".java",
                "typescript" => ".ts",
                "javascript" => ".js",
                "python" => ".py",
                "go" => ".go",
                _ => throw new ArgumentException($"Unsupported language: '{language}'. Supported languages are: dotnet, java, typescript, python, go", nameof(language))
            };
        }

        /// <summary>
        /// Normalizes a language identifier to lowercase and converts javascript to typescript.
        /// </summary>
        /// <param name="language">The language identifier to normalize</param>
        /// <returns>The normalized language identifier</returns>
        private static string NormalizeLanguage(string language)
        {
            var normalized = language.ToLowerInvariant();
            if (normalized == "javascript")
            {
                return "typescript";
            }

            return normalized;
        }

        /// <summary>
        /// Determines if a file matches any of the specified extensions, including compound extensions like .d.ts.
        /// </summary>
        /// <param name="filePath">The file path to check</param>
        /// <param name="extensions">The extensions to match against</param>
        /// <returns>True if the file matches any extension, false otherwise</returns>
        private static bool MatchesAnyExtension(string filePath, ISet<string> extensions)
        {
            var fileName = Path.GetFileName(filePath);
            
            // Check each extension to see if the filename ends with it
            foreach (var extension in extensions)
            {
                if (fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Represents metadata about a discovered file.
        /// </summary>
        /// <param name="FilePath">Full path to the file</param>
        /// <param name="RelativePath">Relative path for display purposes</param>
        /// <param name="FileSize">Total size of the file in bytes</param>
        /// <param name="Priority">Priority for inclusion (lower numbers = higher priority)</param>
        public record FileMetadata(
            string FilePath,
            string RelativePath,
            int FileSize,
            int Priority
        );

        /// <summary>
        /// Represents an input specification with its own filtering rules.
        /// </summary>
        /// <param name="Path">File or directory path to include</param>
        /// <param name="IncludeExtensions">File extensions to include for this input (e.g., [".cs", ".ts"]). If null, all extensions are included.</param>
        /// <param name="ExcludeGlobPatterns">Glob patterns for paths to exclude for this input (e.g., ["**/test/**", "**/bin/**"]). If null, no exclusions are applied.</param>
        public record SourceInput(
            string Path,
            string[]? IncludeExtensions = null,
            string[]? ExcludeGlobPatterns = null
        );

        /// <summary>
        /// Represents an individual file in a loading plan.
        /// </summary>
        /// <param name="FilePath">Full path to the file</param>
        /// <param name="RelativePath">Relative path for display purposes</param>
        /// <param name="FileSize">Total size of the file in bytes</param>
        /// <param name="ContentToLoad">Number of characters to load from this file</param>
        /// <param name="EstimatedTokens">Estimated token count for the content to be loaded</param>
        /// <param name="IsTruncated">Whether the file content will be truncated</param>
        public record FileLoadingItem(
            string FilePath,
            string RelativePath,
            int FileSize,
            int ContentToLoad,
            int EstimatedTokens,
            bool IsTruncated
        );

        /// <summary>
        /// Represents a plan for loading files with budget allocation.
        /// </summary>
        /// <param name="Items">List of files to load with their allocations</param>
        /// <param name="TotalFilesFound">Total number of files discovered</param>
        /// <param name="TotalFilesIncluded">Number of files included in the plan</param>
        /// <param name="TotalEstimatedTokens">Total estimated tokens for all included files</param>
        /// <param name="BudgetUsed">Amount of character budget used</param>
        /// <param name="TotalBudget">Total character budget available</param>
        public record FileLoadingPlan(
            List<FileLoadingItem> Items,
            int TotalFilesFound,
            int TotalFilesIncluded,
            int TotalEstimatedTokens,
            int BudgetUsed,
            int TotalBudget
        );

        /// <summary>
        /// Loads and concatenates files from multiple inputs with individual filtering rules, applying file extension filters and exclusion patterns per input.
        /// </summary>
        /// <param name="inputs">List of inputs, each with its own path and filtering rules</param>
        /// <param name="relativeTo">Base path for computing relative file paths in output</param>
        /// <param name="totalBudget">Maximum characters for all included files</param>
        /// <param name="perFileLimit">Maximum characters per individual file</param>
        /// <param name="priorityFunc">Function to calculate priority for a file (lower numbers = higher priority)</param>
        /// <param name="logger">Optional logger for debugging file loading process</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Concatenated file content with file headers, truncated to budget</returns>
        public static async Task<string> LoadFilesAsync(
            IEnumerable<SourceInput> inputs,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc,
            ILogger? logger = null,
            CancellationToken ct = default)
        {
            var plan = CreateFileLoadingPlan(inputs, relativeTo, totalBudget, perFileLimit, priorityFunc, logger);
            return await ExecuteFileLoadingPlanAsync(plan, logger, ct);
        }

        /// <summary>
        /// Loads and concatenates files from multiple file paths and directories, applying file extension filters and exclusion patterns.
        /// </summary>
        /// <param name="filePaths">List of file paths and directory paths to scan</param>
        /// <param name="includeExtensions">File extensions to include (e.g., [".cs", ".ts"])</param>
        /// <param name="excludeGlobPatterns">Glob patterns for paths to exclude (e.g., ["**/test/**", "**/bin/**"])</param>
        /// <param name="relativeTo">Base path for computing relative file paths in output</param>
        /// <param name="totalBudget">Maximum characters for all included files</param>
        /// <param name="perFileLimit">Maximum characters per individual file</param>
        /// <param name="priorityFunc">Function to calculate priority for a file (lower numbers = higher priority)</param>
        /// <param name="logger">Optional logger for debugging file loading process</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Concatenated file content with file headers, truncated to budget</returns>
        public static async Task<string> LoadFilesAsync(
            IEnumerable<string> filePaths,
            string[] includeExtensions,
            string[] excludeGlobPatterns,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc,
            ILogger? logger = null,
            CancellationToken ct = default)
        {
            var plan = CreateFileLoadingPlan(filePaths, includeExtensions, excludeGlobPatterns, relativeTo, totalBudget, perFileLimit, priorityFunc, logger);
            return await ExecuteFileLoadingPlanAsync(plan, logger, ct);
        }

        /// <summary>
        /// Loads and concatenates files from a directory, applying file extension filters and exclusion patterns.
        /// </summary>
        /// <param name="dir">Directory to scan for files</param>
        /// <param name="includeExtensions">File extensions to include (e.g., [".cs", ".ts"])</param>
        /// <param name="excludeGlobPatterns">Glob patterns for paths to exclude (e.g., ["**/test/**", "**/bin/**"])</param>
        /// <param name="relativeTo">Base path for computing relative file paths in output</param>
        /// <param name="totalBudget">Maximum characters for all included files</param>
        /// <param name="perFileLimit">Maximum characters per individual file</param>
        /// <param name="priorityFunc">Function to calculate priority for a file (lower numbers = higher priority)</param>
        /// <param name="logger">Optional logger for debugging file loading process</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Concatenated file content with file headers, truncated to budget</returns>
        public static async Task<string> LoadFilesAsync(
            string dir,
            string[] includeExtensions,
            string[] excludeGlobPatterns,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc,
            ILogger? logger = null,
            CancellationToken ct = default)
        {
            return await LoadFilesAsync(
                new[] { dir },
                includeExtensions,
                excludeGlobPatterns,
                relativeTo,
                totalBudget,
                perFileLimit,
                priorityFunc,
                logger,
                ct);
        }

        /// <summary>
        /// Discovers and analyzes all files from multiple inputs with individual filtering rules, returning metadata without budget constraints.
        /// </summary>
        /// <param name="inputs">List of inputs, each with its own path and filtering rules</param>
        /// <param name="relativeTo">Base path for computing relative file paths in output</param>
        /// <param name="priorityFunc">Function to calculate priority for a file (lower numbers = higher priority)</param>
        /// <param name="logger">Optional logger for debugging file discovery process</param>
        /// <returns>List of discovered files with metadata</returns>
        public static List<FileMetadata> DiscoverFiles(
            IEnumerable<SourceInput> inputs,
            string relativeTo,
            Func<FileMetadata, int> priorityFunc,
            ILogger? logger = null)
        {
            var allFiles = new List<FileMetadata>();
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var input in inputs)
            {
                var files = DiscoverFiles(
                    new[] { input.Path },
                    input.IncludeExtensions ?? [],
                    input.ExcludeGlobPatterns ?? [],
                    relativeTo,
                    priorityFunc,
                    logger);

                // Merge files, avoiding duplicates
                foreach (var file in files)
                {
                    if (!processedFiles.Contains(file.FilePath))
                    {
                        allFiles.Add(file);
                        processedFiles.Add(file.FilePath);
                    }
                }
            }

            // Sort the combined results
            allFiles.Sort((a, b) =>
            {
                var priorityComparison = a.Priority.CompareTo(b.Priority);
                return priorityComparison != 0 ? priorityComparison : a.FileSize.CompareTo(b.FileSize);
            });

            logger?.LogDebug("File discovery from inputs completed. Found {fileCount} files total", allFiles.Count);
            return allFiles;
        }

        /// <summary>
        /// Discovers and analyzes all files from multiple file paths and directories, returning metadata without budget constraints.
        /// </summary>
        /// <param name="filePaths">List of file paths and directory paths to scan</param>
        /// <param name="includeExtensions">File extensions to include (e.g., [".cs", ".ts"])</param>
        /// <param name="excludeGlobPatterns">Glob patterns for paths to exclude (e.g., ["**/test/**", "**/bin/**"])</param>
        /// <param name="relativeTo">Base path for computing relative file paths in output</param>
        /// <param name="priorityFunc">Function to calculate priority for a file (lower numbers = higher priority)</param>
        /// <param name="logger">Optional logger for debugging file discovery process</param>
        /// <returns>List of discovered files with metadata</returns>
        public static List<FileMetadata> DiscoverFiles(
            IEnumerable<string> filePaths,
            string[] includeExtensions,
            string[] excludeGlobPatterns,
            string relativeTo,
            Func<FileMetadata, int> priorityFunc,
            ILogger? logger = null)
        {
            var extensionSet = includeExtensions.Length > 0 
                ? new HashSet<string>(includeExtensions, StringComparer.OrdinalIgnoreCase)
                : null; // null means include all extensions
            var fileInfos = new List<FileMetadata>();
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in filePaths)
            {
                var fullPath = Path.GetFullPath(file);

                if (File.Exists(fullPath))
                {
                    // It's a file
                    if (processedFiles.Contains(fullPath))
                    {
                        continue; // Skip duplicates
                    }

                    if ((extensionSet == null || MatchesAnyExtension(fullPath, extensionSet)) &&
                        !IsMatchingExcludePattern(fullPath, excludeGlobPatterns))
                    {
                        var fileInfo = new FileInfo(fullPath);
                        var metadata = new FileMetadata(
                            FilePath: fileInfo.FullName,
                            RelativePath: Path.GetRelativePath(relativeTo, fileInfo.FullName),
                            FileSize: (int)fileInfo.Length,
                            Priority: 0 // Temporary value
                        );

                        metadata = metadata with { Priority = priorityFunc(metadata) };
                        fileInfos.Add(metadata);
                        processedFiles.Add(fullPath);
                    }
                }
                else if (Directory.Exists(fullPath))
                {
                    // It's a directory
                    var directoryFiles = Directory.EnumerateFiles(fullPath, "*.*", SearchOption.AllDirectories).ToArray();
                    logger?.LogDebug("Scanning directory {fullPath}: found {fileCount} files", fullPath, directoryFiles.Length);
                    
                    foreach (var filePath in directoryFiles)
                    {
                        if (processedFiles.Contains(filePath))
                        {
                            continue; // Skip duplicates
                        }

                        // Early filtering to avoid unnecessary FileInfo allocation
                        if ((extensionSet != null && !MatchesAnyExtension(filePath, extensionSet)) ||
                            IsMatchingExcludePattern(filePath, excludeGlobPatterns))
                        {
                            continue;
                        }

                        var fileInfo = new FileInfo(filePath);
                        var metadata = new FileMetadata(
                            FilePath: fileInfo.FullName,
                            RelativePath: Path.GetRelativePath(relativeTo, fileInfo.FullName),
                            FileSize: (int)fileInfo.Length,
                            Priority: 0 // Temporary value
                        );

                        metadata = metadata with { Priority = priorityFunc(metadata) };
                        fileInfos.Add(metadata);
                        processedFiles.Add(filePath);
                    }
                }
                else
                {
                    logger?.LogWarning("Path does not exist: {fullPath}", fullPath);
                }
            }

            // Sort once at the end
            fileInfos.Sort((a, b) =>
            {
                var priorityComparison = a.Priority.CompareTo(b.Priority);
                return priorityComparison != 0 ? priorityComparison : a.FileSize.CompareTo(b.FileSize);
            });

            logger?.LogDebug("File discovery completed. Found {fileCount} files, total size: {totalSize:N0} bytes", 
                fileInfos.Count, 
                fileInfos.Count > 0 ? fileInfos.Sum(f => f.FileSize) : 0);

            return fileInfos;
        }

        /// <summary>
        /// Discovers and analyzes all files in a directory, returning metadata without budget constraints.
        /// </summary>
        /// <param name="dir">Directory to scan for files</param>
        /// <param name="includeExtensions">File extensions to include (e.g., [".cs", ".ts"])</param>
        /// <param name="excludeGlobPatterns">Glob patterns for paths to exclude (e.g., ["**/test/**", "**/bin/**"])</param>
        /// <param name="relativeTo">Base path for computing relative file paths in output</param>
        /// <param name="priorityFunc">Function to calculate priority for a file (lower numbers = higher priority)</param>
        /// <param name="logger">Optional logger for debugging file discovery process</param>
        /// <returns>List of discovered files with metadata</returns>
        public static List<FileMetadata> DiscoverFiles(
            string dir,
            string[] includeExtensions,
            string[] excludeGlobPatterns,
            string relativeTo,
            Func<FileMetadata, int> priorityFunc,
            ILogger? logger = null)
        {
            return DiscoverFiles(
                new[] { dir },
                includeExtensions,
                excludeGlobPatterns,
                relativeTo,
                priorityFunc,
                logger);
        }

        /// <summary>
        /// Creates a budget-constrained loading plan from discovered file metadata.
        /// </summary>
        /// <param name="files">List of discovered files with metadata</param>
        /// <param name="totalBudget">Maximum characters for all included files</param>
        /// <param name="perFileLimit">Maximum characters per individual file</param>
        /// <param name="logger">Optional logger for debugging loading plan creation</param>
        /// <returns>A loading plan with file metadata and token allocations</returns>
        public static FileLoadingPlan CreateLoadingPlanFromMetadata(
            List<FileMetadata> files,
            int totalBudget,
            int perFileLimit,
            ILogger? logger = null)
        {
            var planItems = new List<FileLoadingItem>();
            int remainingBudget = totalBudget;
            const int headerOverhead = 50; // Estimated overhead for "// File: {path}" header per file

            foreach (var file in files)
            {
                if (remainingBudget <= headerOverhead)
                {
                    break; // Not enough budget even for the header
                }

                // Calculate how much content we can load from this file
                var availableBudget = remainingBudget - headerOverhead;
                var contentToLoad = Math.Min(Math.Min(file.FileSize, perFileLimit), availableBudget);

                if (contentToLoad <= 0)
                {
                    break; // No budget left for actual content
                }

                // Rough token estimation: ~4 characters per token for code
                var estimatedTokens = contentToLoad / 4;

                planItems.Add(new FileLoadingItem(
                    FilePath: file.FilePath,
                    RelativePath: file.RelativePath,
                    FileSize: file.FileSize,
                    ContentToLoad: contentToLoad,
                    EstimatedTokens: estimatedTokens,
                    IsTruncated: contentToLoad < file.FileSize
                ));

                remainingBudget -= (contentToLoad + headerOverhead);
            }

            logger?.LogDebug("Loading plan created: {includedFiles}/{totalFiles} files, {usedBudget}/{totalBudget} budget used", 
                planItems.Count, files.Count, totalBudget - remainingBudget, totalBudget);

            return new FileLoadingPlan(
                Items: planItems,
                TotalFilesFound: files.Count,
                TotalFilesIncluded: planItems.Count,
                TotalEstimatedTokens: planItems.Sum(item => item.EstimatedTokens),
                BudgetUsed: totalBudget - remainingBudget,
                TotalBudget: totalBudget
            );
        }

        /// <summary>
        /// Creates a plan for loading files from multiple inputs with individual filtering rules, specifying which files to include and how much content to load from each.
        /// </summary>
        /// <param name="inputs">List of inputs, each with its own path and filtering rules</param>
        /// <param name="relativeTo">Base path for computing relative file paths in output</param>
        /// <param name="totalBudget">Maximum characters for all included files</param>
        /// <param name="perFileLimit">Maximum characters per individual file</param>
        /// <param name="priorityFunc">Function to calculate priority for a file (lower numbers = higher priority)</param>
        /// <param name="logger">Optional logger for debugging file loading plan creation</param>
        /// <returns>A loading plan with file metadata and token allocations</returns>
        public static FileLoadingPlan CreateFileLoadingPlan(
            IEnumerable<SourceInput> inputs,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc,
            ILogger? logger = null)
        {
            var files = DiscoverFiles(inputs, relativeTo, priorityFunc, logger);
            var plan = CreateLoadingPlanFromMetadata(files, totalBudget, perFileLimit, logger);
            logger?.LogDebug("Created loading plan from inputs: {totalFiles} files found, {includedFiles} files included, {budgetUsed}/{totalBudget} budget used", 
                plan.TotalFilesFound, plan.TotalFilesIncluded, plan.BudgetUsed, plan.TotalBudget);
            return plan;
        }

        /// <summary>
        /// Creates a plan for loading files from multiple file paths and directories, specifying which files to include and how much content to load from each.
        /// </summary>
        /// <param name="filePaths">List of file paths and directory paths to scan</param>
        /// <param name="includeExtensions">File extensions to include (e.g., [".cs", ".ts"])</param>
        /// <param name="excludeGlobPatterns">Glob patterns for paths to exclude (e.g., ["**/test/**", "**/bin/**"])</param>
        /// <param name="relativeTo">Base path for computing relative file paths in output</param>
        /// <param name="totalBudget">Maximum characters for all included files</param>
        /// <param name="perFileLimit">Maximum characters per individual file</param>
        /// <param name="priorityFunc">Function to calculate priority for a file (lower numbers = higher priority)</param>
        /// <param name="logger">Optional logger for debugging file loading plan creation</param>
        /// <returns>A loading plan with file metadata and token allocations</returns>
        public static FileLoadingPlan CreateFileLoadingPlan(
            IEnumerable<string> filePaths,
            string[] includeExtensions,
            string[] excludeGlobPatterns,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc,
            ILogger? logger = null)
        {
            var files = DiscoverFiles(filePaths, includeExtensions, excludeGlobPatterns, relativeTo, priorityFunc, logger);
            var plan = CreateLoadingPlanFromMetadata(files, totalBudget, perFileLimit, logger);
            logger?.LogDebug("Created loading plan: {totalFiles} files found, {includedFiles} files included, {budgetUsed}/{totalBudget} budget used", 
                plan.TotalFilesFound, plan.TotalFilesIncluded, plan.BudgetUsed, plan.TotalBudget);
            return plan;
        }

        /// <summary>
        /// Creates a plan for loading files, specifying which files to include and how much content to load from each.
        /// </summary>
        /// <param name="dir">Directory to scan for files</param>
        /// <param name="includeExtensions">File extensions to include (e.g., [".cs", ".ts"])</param>
        /// <param name="excludeGlobPatterns">Glob patterns for paths to exclude (e.g., ["**/test/**", "**/bin/**"])</param>
        /// <param name="relativeTo">Base path for computing relative file paths in output</param>
        /// <param name="totalBudget">Maximum characters for all included files</param>
        /// <param name="perFileLimit">Maximum characters per individual file</param>
        /// <param name="priorityFunc">Function to calculate priority for a file (lower numbers = higher priority)</param>
        /// <param name="logger">Optional logger for debugging file loading plan creation</param>
        /// <returns>A loading plan with file metadata and token allocations</returns>
        public static FileLoadingPlan CreateFileLoadingPlan(
            string dir,
            string[] includeExtensions,
            string[] excludeGlobPatterns,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc,
            ILogger? logger = null)
        {
            return CreateFileLoadingPlan(
                new[] { dir },
                includeExtensions,
                excludeGlobPatterns,
                relativeTo,
                totalBudget,
                perFileLimit,
                priorityFunc,
                logger);
        }

        /// <summary>
        /// Executes a file loading plan to produce the concatenated content.
        /// </summary>
        /// <param name="plan">The loading plan to execute</param>
        /// <param name="logger">Optional logger for debugging file loading execution</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Concatenated file content with file headers</returns>
        public static async Task<string> ExecuteFileLoadingPlanAsync(FileLoadingPlan plan, ILogger? logger = null, CancellationToken ct = default)
        {
            if (plan.Items.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            int processedFiles = 0;
            int failedFiles = 0;

            foreach (var item in plan.Items)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(item.FilePath, ct);

                    // Truncate to planned amount
                    if (content.Length > item.ContentToLoad)
                    {
                        content = content.Substring(0, item.ContentToLoad) + "\n// ... truncated ...";
                    }

                    sb.AppendLine($"<file path=\"{item.RelativePath}\" size=\"{item.FileSize}\" loading=\"{item.ContentToLoad}\" tokens=\"{item.EstimatedTokens}\">");
                    sb.AppendLine(content);
                    sb.AppendLine("</file>");
                    sb.AppendLine();
                    processedFiles++;
                }
                catch (Exception ex)
                {
                    // Log but continue with other files
                    logger?.LogWarning("Failed to load file {relativePath}: {error}", item.RelativePath, ex.Message);
                    sb.AppendLine($"<file path=\"{item.RelativePath}\" error=\"{ex.Message}\" />");
                    sb.AppendLine();
                    failedFiles++;
                }
            }

            logger?.LogDebug("File loading completed: {processedFiles} processed, {failedFiles} failed", processedFiles, failedFiles);

            if (plan.TotalFilesFound > plan.TotalFilesIncluded)
            {
                var omitted = plan.TotalFilesFound - plan.TotalFilesIncluded;
                sb.AppendLine($"// {omitted} additional files omitted due to size budget.");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Checks if a file path matches any of the provided glob exclusion patterns.
        /// </summary>
        /// <param name="filePath">File path to check</param>
        /// <param name="excludeGlobPatterns">Array of glob patterns (e.g., "**/test/**", "*.tmp", "bin/**")</param>
        /// <returns>True if the path should be excluded</returns>
        public static bool IsMatchingExcludePattern(string filePath, string[] excludeGlobPatterns)
        {
            if (excludeGlobPatterns == null || excludeGlobPatterns.Length == 0)
            {
                return false;
            }

            // Normalize path separators to forward slashes for consistent globbing
            var normalizedPath = filePath.Replace(Path.DirectorySeparatorChar, '/');
            
            // Create a matcher and add all exclude patterns
            var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            matcher.AddIncludePatterns(excludeGlobPatterns);

            // Check if the normalized path matches any of the exclude patterns
            var result = matcher.Match(normalizedPath);
            return result.HasMatches;
        }
    }
}
