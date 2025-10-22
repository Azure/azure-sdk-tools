// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    /// <summary>
    /// Helper class for file and directory operations.
    /// </summary>
    public class FileHelper : IFileHelper
    {
        private readonly ILogger<FileHelper> _logger;

        public FileHelper(ILogger<FileHelper> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validates that the specified directory exists and is empty.
        /// </summary>
        /// <param name="dir">The directory path to validate.</param>
        /// <returns>An error message if validation fails, or null if validation passes.</returns>
        public string? ValidateEmptyDirectory(string dir)
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
        /// Optimized extension matching using ReadOnlySpan to avoid string allocations.
        /// </summary>
        /// <param name="fileName">The file name as a span</param>
        /// <param name="extensions">The extensions to match against</param>
        /// <returns>True if the file matches any extension, false otherwise</returns>
        private static bool MatchesAnyExtensionSpan(ReadOnlySpan<char> fileName, ISet<string> extensions)
        {
            foreach (var extension in extensions)
            {
                if (fileName.EndsWith(extension.AsSpan(), StringComparison.OrdinalIgnoreCase))
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
        /// Discovers, plans, and loads source files described by a set of <see cref="SourceInput"/> entries, applying per-input
        /// extension and glob-based exclusion rules. This is the highest-level convenience overload.
        /// </summary>
        /// <param name="inputs">A collection of inputs describing one or more files or directories plus optional filtering rules.</param>
        /// <param name="relativeTo">Root path used to compute stable relative paths for output. Should normally be the repository root.</param>
        /// <param name="totalBudget">Maximum number of characters across all included file contents (exclusive of some minor header overhead).</param>
        /// <param name="perFileLimit">Maximum number of characters permitted for any single file before truncation is applied.</param>
        /// <param name="priorityFunc">A deterministic function returning an integer priority for each file (lower values are loaded first). Ties are broken by smaller file size.</param>
        /// <param name="logger">Optional logger for tracing discovery and loading decisions. Uses Debug for summary and Trace for per-file discovery.</param>
        /// <param name="ct">Cancellation token that aborts file content reads. Discovery and planning are synchronous and ignore cancellation.</param>
        /// <returns>A single string containing XML-like <c>&lt;file&gt;</c> blocks for each included file. Truncated files append a <c>// ... truncated ...</c> marker. Files that failed to load produce a self-closing <c>&lt;file error=.../&gt;</c> element.</returns>
        /// <remarks>
        /// Processing pipeline:
        /// <list type="number">
        /// <item><description>Discover files per input (deduplicated, extension &amp; glob exclusions applied).</description></item>
        /// <item><description>Create a loading plan honoring <paramref name="totalBudget"/> and <paramref name="perFileLimit"/>.</description></item>
        /// <item><description>Execute the plan, streaming partial file content when truncation is needed.</description></item>
        /// </list>
        /// Edge cases &amp; behavior:
        /// <list type="bullet">
        /// <item><description>If no files qualify, returns <see cref="string.Empty"/>.</description></item>
        /// <item><description>IO and parsing errors for individual files are captured; the exception message is surfaced in an error element without failing the entire operation.</description></item>
        /// <item><description>Character budget accounting includes an estimated 50 character header overhead per file (see implementation constant).</description></item>
        /// <item><description>The output ordering is strictly by ascending priority then ascending file size.</description></item>
        /// </list>
        /// Recommended usage: supply a lightweight <paramref name="priorityFunc"/> (e.g. categorize by path prefixes) to avoid perf costs during large repository scans.
        /// </remarks>
        public async Task<string> LoadFilesAsync(
            IEnumerable<SourceInput> inputs,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc,
            CancellationToken ct = default)
        {
            var plan = CreateFileLoadingPlan(inputs, relativeTo, totalBudget, perFileLimit, priorityFunc);
            return await ExecuteFileLoadingPlanAsync(plan, ct);
        }

        /// <summary>
        /// Discovers, plans, and loads source files specified directly by explicit paths (files and/or directories).
        /// </summary>
        /// <param name="filePaths">A collection of file or directory paths. Directories are scanned recursively.</param>
        /// <param name="includeExtensions">Case-insensitive set of extensions to include (e.g. <c>[".cs", ".ts"]</c>). Provide an empty array to include all extensions.</param>
        /// <param name="excludeGlobPatterns">Glob patterns evaluated against the path relative to <paramref name="relativeTo"/>. Provide an empty array for no exclusions.</param>
        /// <param name="relativeTo">Base path used to compute relative output paths and as the root for glob evaluation.</param>
        /// <param name="totalBudget">Maximum aggregate character budget for loaded content.</param>
        /// <param name="perFileLimit">Maximum per-file character allocation before truncation occurs.</param>
        /// <param name="priorityFunc">Function computing file priority (lower loads first). Should be inexpensive; called once per candidate.</param>
        /// <param name="logger">Optional logger for discovery and planning diagnostics.</param>
        /// <param name="ct">Cancellation token applied only during file reads.</param>
        /// <returns>A concatenated representation of included files in XML-ish blocks with metadata attributes.</returns>
        /// <remarks>
        /// Behaves identically to the <see cref="LoadFilesAsync(IEnumerable{SourceInput}, string, int, int, Func{FileMetadata, int}, ILogger?, CancellationToken)"/> overload except that filtering rules are shared across all <paramref name="filePaths"/>.
        /// </remarks>
        public async Task<string> LoadFilesAsync(
            IEnumerable<string> filePaths,
            string[] includeExtensions,
            string[] excludeGlobPatterns,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc,
            CancellationToken ct = default)
        {
            var plan = CreateFileLoadingPlan(filePaths, includeExtensions, excludeGlobPatterns, relativeTo, totalBudget, perFileLimit, priorityFunc);
            return await ExecuteFileLoadingPlanAsync(plan, ct);
        }

        /// <summary>
        /// Convenience overload for loading files from a single directory using shared filtering rules.
        /// </summary>
        /// <param name="dir">Directory to scan recursively.</param>
        /// <param name="includeExtensions">Extensions to include or empty array for all.</param>
        /// <param name="excludeGlobPatterns">Glob exclusion patterns relative to <paramref name="relativeTo"/>.</param>
        /// <param name="relativeTo">Root path for relative output formatting.</param>
        /// <param name="totalBudget">Aggregate character budget across all files.</param>
        /// <param name="perFileLimit">Maximum characters per file before truncation.</param>
        /// <param name="priorityFunc">Priority selector (lower first); see remarks in other overloads.</param>
        /// <param name="logger">Optional logger.</param>
        /// <param name="ct">Cancellation token applied during file reads only.</param>
        /// <returns>Concatenated structured content string for included files.</returns>
        public async Task<string> LoadFilesAsync(
            string dir,
            string[] includeExtensions,
            string[] excludeGlobPatterns,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc,
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
                ct);
        }

        /// <summary>
        /// Discovers candidate files from multiple <see cref="SourceInput"/> definitions applying per-input include extension
        /// and exclusion glob patterns, returning ordered metadata without any budgeting applied.
        /// </summary>
        /// <param name="inputs">Input descriptors (each may specify its own include and exclude patterns).</param>
        /// <param name="relativeTo">Root path used for computing human-readable <see cref="FileMetadata.RelativePath"/> values.</param>
        /// <param name="priorityFunc">Function computing relative priority (lower loads first). Invoked once per discovered file.</param>
        /// <param name="logger">Optional logger used for summary information and Trace-level per-file discovery.</param>
        /// <returns>Sorted list of unique file metadata entries across all inputs.</returns>
        /// <remarks>
        /// Duplicates (same full path) are removed across inputs. Sorting occurs by ascending priority then ascending file size.
        /// </remarks>
        public List<FileMetadata> DiscoverFiles(
            IEnumerable<SourceInput> inputs,
            string relativeTo,
            Func<FileMetadata, int> priorityFunc)
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
                    priorityFunc);

                foreach (var file in files)
                {
                    if (!processedFiles.Contains(file.FilePath))
                    {
                        allFiles.Add(file);
                        processedFiles.Add(file.FilePath);
                    }
                }
            }

            allFiles.Sort((a, b) =>
            {
                var priorityComparison = a.Priority.CompareTo(b.Priority);
                return priorityComparison != 0 ? priorityComparison : a.FileSize.CompareTo(b.FileSize);
            });

            _logger.LogDebug("File discovery from inputs completed. Found {fileCount} files total", allFiles.Count);
            if (allFiles.Count > 0 && _logger.IsEnabled(LogLevel.Debug))
            {
                var previewCount = Math.Min(10, allFiles.Count);
                var preview = string.Join(", ", allFiles.Take(previewCount).Select(f => f.RelativePath));
                if (allFiles.Count > previewCount)
                {
                    _logger.LogDebug("First {previewCount} files: {preview} ... (+{remaining} more)", previewCount, preview, allFiles.Count - previewCount);
                }
                else
                {
                    _logger.LogDebug("Discovered files: {preview}", preview);
                }
            }
            return allFiles;
        }

        /// <summary>
        /// Discovers candidate files from explicit paths (files and directories) applying shared include extension and exclusion glob patterns.
        /// </summary>
        /// <param name="filePaths">File or directory paths. Non-existent paths produce a warning entry via <paramref name="logger"/>.</param>
        /// <param name="includeExtensions">Extensions to include (empty array = include all). Case-insensitive match.</param>
        /// <param name="excludeGlobPatterns">Glob exclusion patterns evaluated against path relative to <paramref name="relativeTo"/>.</param>
        /// <param name="relativeTo">Base path for computing relative output paths and performing glob evaluation.</param>
        /// <param name="priorityFunc">Function computing file priority (lower is higher priority).</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>Sorted list of unique file metadata entries.</returns>
        /// <remarks>
        /// Directory enumeration uses <see cref="EnumerationOptions"/> with recursion enabled and skips hidden/system entries.
        /// Individual file access failures during size retrieval will throw; there is no suppression at discovery time.
        /// </remarks>
        public List<FileMetadata> DiscoverFiles(
            IEnumerable<string> filePaths,
            string[] includeExtensions,
            string[] excludeGlobPatterns,
            string relativeTo,
            Func<FileMetadata, int> priorityFunc)
        {
            var extensionSet = includeExtensions.Length > 0
                ? new HashSet<string>(includeExtensions, StringComparer.OrdinalIgnoreCase)
                : null;
            var fileInfos = new List<FileMetadata>();
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Matcher? excludeMatcher = null;
            if (excludeGlobPatterns.Length > 0)
            {
                excludeMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                excludeMatcher.AddIncludePatterns(excludeGlobPatterns);
            }

            foreach (var file in filePaths)
            {
                var fullPath = Path.GetFullPath(file);

                if (File.Exists(fullPath))
                {
                    TryProcessFile(fullPath, extensionSet, excludeMatcher, relativeTo, priorityFunc, fileInfos, processedFiles);
                }
                else if (Directory.Exists(fullPath))
                {
                    _logger.LogDebug("Scanning directory {fullPath}", fullPath);

                    var enumerationOptions = new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
                    };

                    foreach (var filePath in Directory.EnumerateFiles(fullPath, "*.*", enumerationOptions))
                    {
                        TryProcessFile(filePath, extensionSet, excludeMatcher, relativeTo, priorityFunc, fileInfos, processedFiles);
                    }
                }
                else
                {
                    _logger.LogWarning("Path does not exist: {fullPath}", fullPath);
                }
            }

            fileInfos.Sort((a, b) =>
            {
                var priorityComparison = a.Priority.CompareTo(b.Priority);
                return priorityComparison != 0 ? priorityComparison : a.FileSize.CompareTo(b.FileSize);
            });

            _logger.LogDebug("File discovery completed. Found {fileCount} files, total size: {totalSize:N0} bytes",
                fileInfos.Count,
                fileInfos.Count > 0 ? fileInfos.Sum(f => f.FileSize) : 0);

            if (fileInfos.Count > 0 && _logger.IsEnabled(LogLevel.Debug))
            {
                var previewCount = Math.Min(10, fileInfos.Count);
                var preview = string.Join(", ", fileInfos.Take(previewCount).Select(f => f.RelativePath));
                if (fileInfos.Count > previewCount)
                {
                    _logger.LogDebug("First {previewCount} files: {preview} ... (+{remaining} more)", previewCount, preview, fileInfos.Count - previewCount);
                }
                else
                {
                    _logger.LogDebug("Discovered files: {preview}", preview);
                }
            }

            return fileInfos;
        }

        /// <summary>
        /// Convenience overload for discovering files from a single directory.
        /// </summary>
        /// <param name="dir">Directory path to scan recursively.</param>
        /// <param name="includeExtensions">Extensions filter (empty = all).</param>
        /// <param name="excludeGlobPatterns">Glob exclusions relative to <paramref name="relativeTo"/>.</param>
        /// <param name="relativeTo">Root path for relative path calculation.</param>
        /// <param name="priorityFunc">Priority function.</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>Sorted list of discovered file metadata.</returns>
        public List<FileMetadata> DiscoverFiles(
            string dir,
            string[] includeExtensions,
            string[] excludeGlobPatterns,
            string relativeTo,
            Func<FileMetadata, int> priorityFunc)
        {
            return DiscoverFiles(
                new[] { dir },
                includeExtensions,
                excludeGlobPatterns,
                relativeTo,
                priorityFunc);
        }

        /// <summary>
        /// Produces a <see cref="FileLoadingPlan"/> from raw file metadata honoring the provided total and per-file character budgets.
        /// </summary>
        /// <param name="files">Pre-sorted file metadata (priority ascending then size ascending).</param>
        /// <param name="totalBudget">Maximum aggregate character budget for all file contents plus header overhead.</param>
        /// <param name="perFileLimit">Maximum characters allocated to any single file before truncation.</param>
        /// <param name="logger">Optional logger for plan diagnostics.</param>
        /// <returns>A plan describing which files to load and how many characters per file.</returns>
        /// <remarks>
        /// Implementation details:
        /// <list type="bullet">
        /// <item><description>Reserves an estimated 50 characters of overhead per file for the output header (constant).</description></item>
        /// <item><description>Stops allocation when remaining budget cannot cover another header + at least 1 character.</description></item>
        /// <item><description>Token estimation uses a simple 4 characters ≈ 1 token heuristic suitable for coarse budgeting.</description></item>
        /// </list>
        /// </remarks>
        public FileLoadingPlan CreateLoadingPlanFromMetadata(
            List<FileMetadata> files,
            int totalBudget,
            int perFileLimit)
        {
            var planItems = new List<FileLoadingItem>();
            int remainingBudget = totalBudget;
            const int headerOverhead = 50; // Estimated overhead for "// File: {path}" header per file

            foreach (var file in files)
            {
                if (remainingBudget <= headerOverhead)
                {
                    break;
                }

                var availableBudget = remainingBudget - headerOverhead;
                var contentToLoad = Math.Min(Math.Min(file.FileSize, perFileLimit), availableBudget);

                if (contentToLoad <= 0)
                {
                    break;
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

                remainingBudget -= contentToLoad + headerOverhead;
            }

            _logger.LogDebug("Loading plan created: {includedFiles}/{totalFiles} files, {usedBudget}/{totalBudget} budget used", 
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
        /// Discovers files from multiple <see cref="SourceInput"/> entries and produces a constrained loading plan.
        /// </summary>
        /// <param name="inputs">Inputs describing paths and per-input filters.</param>
        /// <param name="relativeTo">Root path for relative path computation.</param>
        /// <param name="totalBudget">Aggregate character budget.</param>
        /// <param name="perFileLimit">Per-file character limit.</param>
        /// <param name="priorityFunc">Priority selector (lower first).</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>Loading plan ready for execution.</returns>
        public FileLoadingPlan CreateFileLoadingPlan(
            IEnumerable<SourceInput> inputs,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc)
        {
            var files = DiscoverFiles(inputs, relativeTo, priorityFunc);
            var plan = CreateLoadingPlanFromMetadata(files, totalBudget, perFileLimit);
            _logger.LogDebug("Created loading plan from inputs: {totalFiles} files found, {includedFiles} files included, {budgetUsed}/{totalBudget} budget used", 
                plan.TotalFilesFound, plan.TotalFilesIncluded, plan.BudgetUsed, plan.TotalBudget);
            return plan;
        }

        /// <summary>
        /// Discovers files from explicit paths and produces a constrained loading plan.
        /// </summary>
        /// <param name="filePaths">File and/or directory paths.</param>
        /// <param name="includeExtensions">Extensions filter; empty for all.</param>
        /// <param name="excludeGlobPatterns">Glob exclusion patterns (relative to <paramref name="relativeTo"/>).</param>
        /// <param name="relativeTo">Root path for relative path computation.</param>
        /// <param name="totalBudget">Aggregate character budget.</param>
        /// <param name="perFileLimit">Per-file character limit.</param>
        /// <param name="priorityFunc">Priority selector (lower first).</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>Loading plan ready for execution.</returns>
        public FileLoadingPlan CreateFileLoadingPlan(
            IEnumerable<string> filePaths,
            string[] includeExtensions,
            string[] excludeGlobPatterns,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc)
        {
            var files = DiscoverFiles(filePaths, includeExtensions, excludeGlobPatterns, relativeTo, priorityFunc);
            var plan = CreateLoadingPlanFromMetadata(files, totalBudget, perFileLimit);
            _logger.LogDebug("Created loading plan: {totalFiles} files found, {includedFiles} files included, {budgetUsed}/{totalBudget} budget used", 
                plan.TotalFilesFound, plan.TotalFilesIncluded, plan.BudgetUsed, plan.TotalBudget);
            return plan;
        }

        /// <summary>
        /// Convenience overload for producing a loading plan from a single directory.
        /// </summary>
        /// <param name="dir">Directory to scan recursively.</param>
        /// <param name="includeExtensions">Extensions filter; empty array for all.</param>
        /// <param name="excludeGlobPatterns">Glob exclusion patterns relative to <paramref name="relativeTo"/>.</param>
        /// <param name="relativeTo">Root path for relative path computation.</param>
        /// <param name="totalBudget">Aggregate character budget.</param>
        /// <param name="perFileLimit">Per-file character limit.</param>
        /// <param name="priorityFunc">Priority selector (lower first).</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>Loading plan ready for execution.</returns>
        public FileLoadingPlan CreateFileLoadingPlan(
            string dir,
            string[] includeExtensions,
            string[] excludeGlobPatterns,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc)
        {
            return CreateFileLoadingPlan(
                new[] { dir },
                includeExtensions,
                excludeGlobPatterns,
                relativeTo,
                totalBudget,
                perFileLimit,
                priorityFunc);
        }

    /// <summary>
    /// Executes a previously prepared <see cref="FileLoadingPlan"/>, reading file contents (subject to truncation) and
    /// emitting a concatenated XML-like representation suitable for downstream processing or prompting.
    /// </summary>
    /// <param name="plan">Plan created by <see cref="CreateFileLoadingPlan"/> or <see cref="CreateLoadingPlanFromMetadata"/>.</param>
    /// <param name="logger">Optional logger for progress and warnings during read.</param>
    /// <param name="ct">Cancellation token that aborts file reads; already processed content is retained.</param>
    /// <returns>Structured concatenated content or empty string if no files are included.</returns>
    /// <remarks>
    /// Individual file read failures do not throw; instead an error element is written. This design favors resilience
    /// for large scans where sporadic IO issues may occur. Partial reads (truncation) append a human-readable marker.
    /// </remarks>
        public async Task<string> ExecuteFileLoadingPlanAsync(FileLoadingPlan plan, CancellationToken ct = default)
        {
            if (plan.Items.Count == 0)
            {
                return string.Empty;
            }

            var estimatedCapacity = plan.BudgetUsed + (plan.Items.Count * 100);
            var sb = new StringBuilder(estimatedCapacity);
            int processedFiles = 0;
            int failedFiles = 0;

            foreach (var item in plan.Items)
            {
                try
                {
                    string content;
                    
                    if (item.ContentToLoad >= item.FileSize)
                    {
                        content = await File.ReadAllTextAsync(item.FilePath, ct);
                    }
                    else
                    {
                        // Read only needed portion using buffer
                        using var stream = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        
                        var buffer = new char[item.ContentToLoad];
                        var charsRead = await reader.ReadAsync(buffer.AsMemory(0, item.ContentToLoad), ct);
                        content = new string(buffer.AsSpan(0, charsRead));
                        
                        if (charsRead == item.ContentToLoad && !reader.EndOfStream)
                        {
                            content += "\n// ... truncated ...";
                        }
                    }

                    sb.AppendLine($"<file path=\"{item.RelativePath}\" size=\"{item.FileSize}\" loading=\"{item.ContentToLoad}\" tokens=\"{item.EstimatedTokens}\">");
                    sb.AppendLine(content);
                    sb.AppendLine("</file>");
                    sb.AppendLine();
                    processedFiles++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to load file {relativePath}: {error}", item.RelativePath, ex.Message);
                    sb.AppendLine($"<file path=\"{item.RelativePath}\" error=\"{ex.Message}\" />");
                    sb.AppendLine();
                    failedFiles++;
                }
            }

            _logger.LogDebug("File loading completed: {processedFiles} processed, {failedFiles} failed", processedFiles, failedFiles);

            if (plan.TotalFilesFound > plan.TotalFilesIncluded)
            {
                var omitted = plan.TotalFilesFound - plan.TotalFilesIncluded;
                sb.AppendLine($"// {omitted} additional files omitted due to size budget.");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Processes a single file if it meets the filtering criteria and hasn't been processed already.
        /// </summary>
        /// <param name="filePath">Full path to the file to process</param>
        /// <param name="extensionSet">Set of allowed extensions, or null to allow all</param>
        /// <param name="excludeGlobPatterns">Glob patterns for exclusion</param>
        /// <param name="relativeTo">Base path for computing relative paths</param>
        /// <param name="priorityFunc">Function to calculate file priority</param>
        /// <param name="fileInfos">List to add the file metadata to</param>
        /// <param name="processedFiles">Set to track processed files and avoid duplicates</param>
        private void TryProcessFile(
            string filePath,
            ISet<string>? extensionSet,
            Matcher? excludeMatcher,
            string relativeTo,
            Func<FileMetadata, int> priorityFunc,
            List<FileMetadata> fileInfos,
            HashSet<string> processedFiles)
        {
            if (processedFiles.Contains(filePath))
            {
                return;
            }

            var filePathSpan = filePath.AsSpan();
            var fileName = Path.GetFileName(filePathSpan);

            if (extensionSet != null && !MatchesAnyExtensionSpan(fileName, extensionSet))
            {
                return;
            }

            // Match exclude patterns against a path relative to the provided root.
            if (excludeMatcher != null)
            {
                var globPath = Path.GetRelativePath(relativeTo, filePath).Replace(Path.DirectorySeparatorChar, '/');
                if (excludeMatcher.Match(globPath).HasMatches)
                {
                    return;
                }
            }

            var fileInfo = new FileInfo(filePath);
            var metadata = new FileMetadata(
                FilePath: fileInfo.FullName,
                RelativePath: Path.GetRelativePath(relativeTo, fileInfo.FullName),
                FileSize: (int)fileInfo.Length,
                Priority: priorityFunc(new FileMetadata(fileInfo.FullName, Path.GetRelativePath(relativeTo, fileInfo.FullName), (int)fileInfo.Length, 0))
            );

            fileInfos.Add(metadata);
            processedFiles.Add(filePath);

            // Use very fine-grained level so normal info/debug users don't see per-file chatter.
            _logger.LogTrace("Discovered file {relativePath} (size {size} bytes, priority {priority})", 
                metadata.RelativePath, metadata.FileSize, metadata.Priority);
        }

    }
}
