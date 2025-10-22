// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers
{
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
        /// Discovers, plans, and loads source files described by a set of <see cref="FileHelper.SourceInput"/> entries, applying per-input
        /// extension and glob-based exclusion rules. This is the highest-level convenience overload.
        /// </summary>
        Task<string> LoadFilesAsync(
            IEnumerable<FileHelper.SourceInput> inputs,
            string relativeTo,
            int totalBudget,
            int perFileLimit,
            Func<FileHelper.FileMetadata, int> priorityFunc,
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
            Func<FileHelper.FileMetadata, int> priorityFunc,
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
            Func<FileHelper.FileMetadata, int> priorityFunc,
            CancellationToken ct = default);
    }
}
