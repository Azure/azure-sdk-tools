// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

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
        /// Creates a timestamped backup directory within the specified base directory.
        /// The backup directory name follows the pattern "backup-yyyyMMdd_HHmmss".
        /// </summary>
        /// <param name="baseDirectory">The base directory where backup should be created.</param>
        /// <returns>The path to the created timestamped backup directory.</returns>
        public static string CreateTimestampedBackupDirectory(string baseDirectory)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupDirName = $"backup-{timestamp}";
            var backupPath = Path.Combine(baseDirectory, backupDirName);
            Directory.CreateDirectory(backupPath);
            return backupPath;
        }

        /// <summary>
        /// Copies all files from source to target directory recursively.
        /// Uses the built-in File.Copy for better performance and reliability.
        /// </summary>
        /// <param name="source">Source directory.</param>
        /// <param name="target">Target directory.</param>
        /// <param name="ct">Cancellation token.</param>
        public static void CopyDirectory(DirectoryInfo source, DirectoryInfo target, CancellationToken ct = default)
        {
            if (!target.Exists)
            {
                target.Create();
            }

            // Copy all files using built-in File.Copy
            foreach (var file in source.GetFiles())
            {
                ct.ThrowIfCancellationRequested();
                var targetFilePath = Path.Combine(target.FullName, file.Name);
                File.Copy(file.FullName, targetFilePath, overwrite: true);
            }

            // Recursively copy subdirectories (excluding backup directories)
            var subDirectories = source.GetDirectories()
                .Where(d => !d.Name.StartsWith("backup-", StringComparison.OrdinalIgnoreCase));

            foreach (var subDir in subDirectories)
            {
                ct.ThrowIfCancellationRequested();
                var targetSubDir = target.CreateSubdirectory(subDir.Name);
                CopyDirectory(subDir, targetSubDir, ct);
            }
        }
    }
}
