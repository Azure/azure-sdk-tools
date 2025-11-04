// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers
{
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
    /// Interface for file and directory operations.
    /// </summary>
    public interface IFileHelper
    {
        /// <summary>
        /// Validates that the specified directory exists and is empty.
        /// </summary>
        /// <param name="dir">The directory path to validate.</param>
        /// <returns>An error message if validation fails, or null if validation passes.</returns>
        string? ValidateEmptyDirectory(string dir);

        /// <summary>
        /// Discovers, plans, and loads source files described by a set of <see cref="SourceInput"/> entries, applying per-input
        /// extension and glob-based exclusion rules. This is the highest-level convenience overload.
        /// </summary>
        Task<string> LoadFilesAsync(
            IEnumerable<SourceInput> inputs,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc,
            CancellationToken ct = default);

        /// <summary>
        /// Discovers, plans, and loads source files specified directly by explicit paths (files and/or directories).
        /// </summary>
        Task<string> LoadFilesAsync(
            IEnumerable<string> filePaths,
            string[] includeExtensions,
            string[] excludeGlobPatterns,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc,
            CancellationToken ct = default);

        /// <summary>
        /// Convenience overload for loading files from a single directory using shared filtering rules.
        /// </summary>
        Task<string> LoadFilesAsync(
            string dir,
            string[] includeExtensions,
            string[] excludeGlobPatterns,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileMetadata, int> priorityFunc,
            CancellationToken ct = default);
    }
}
