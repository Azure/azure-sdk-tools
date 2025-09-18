// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;

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
        /// Ascend from the starting path until a .git directory/file is found or the max depth is reached.
        /// Returns the directory path that contains .git, or null if not found.
        /// </summary>
        /// <param name="startPath">Starting path (file or directory).</param>
        /// <param name="maxDepth">Maximum parent traversals.</param>
        public static string? AscendToGitRoot(string startPath, int maxDepth = 12)
        {
            if (string.IsNullOrEmpty(startPath)) return null;

            var current = new DirectoryInfo(Path.GetFullPath(startPath));
            if (!current.Exists) return null;
            // If a file was passed, move to its directory
            if (File.Exists(current.FullName))
            {
                current = new FileInfo(current.FullName).Directory!;
            }

            for (int depth = 0; depth < maxDepth && current != null; depth++)
            {
                var gitPath = Path.Combine(current.FullName, ".git");
                if (Directory.Exists(gitPath) || File.Exists(gitPath))
                {
                    return current.FullName;
                }
                current = current.Parent;
            }
            return null;
        }
    }
}
