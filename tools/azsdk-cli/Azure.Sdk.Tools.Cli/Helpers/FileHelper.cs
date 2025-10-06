// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        /// Creates a temporary backup directory with timestamp.
        /// </summary>
        /// <param name="baseDirectory">The base directory where backup should be created.</param>
        /// <returns>The path to the created backup directory.</returns>
        public static string CreateTempBackupDirectory(string baseDirectory)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupDirName = $"temp-{timestamp}";
            var backupPath = Path.Combine(baseDirectory, backupDirName);
            Directory.CreateDirectory(backupPath);
            return backupPath;
        }

        /// <summary>
        /// Copies all files from source to target directory recursively.
        /// </summary>
        /// <param name="source">Source directory.</param>
        /// <param name="target">Target directory.</param>
        /// <param name="ct">Cancellation token.</param>
        public static async Task CopyDirectoryAsync(DirectoryInfo source, DirectoryInfo target, CancellationToken ct = default)
        {
            if (!target.Exists)
            {
                target.Create();
            }

            // Copy all files
            foreach (var file in source.GetFiles())
            {
                ct.ThrowIfCancellationRequested();
                var targetFile = Path.Combine(target.FullName, file.Name);
                await CopyFileAsync(file.FullName, targetFile, ct);
            }

            // Recursively copy subdirectories (excluding backup directories)
            var subDirectories = source.GetDirectories()
                .Where(d => !d.Name.StartsWith("temp-", StringComparison.OrdinalIgnoreCase));

            foreach (var subDir in subDirectories)
            {
                ct.ThrowIfCancellationRequested();
                var targetSubDir = target.CreateSubdirectory(subDir.Name);
                await CopyDirectoryAsync(subDir, targetSubDir, ct);
            }
        }

        /// <summary>
        /// Copies a file asynchronously.
        /// </summary>
        /// <param name="sourcePath">Source file path.</param>
        /// <param name="targetPath">Target file path.</param>
        /// <param name="ct">Cancellation token.</param>
        private static async Task CopyFileAsync(string sourcePath, string targetPath, CancellationToken ct = default)
        {
            await using var sourceStream = File.OpenRead(sourcePath);
            await using var targetStream = File.Create(targetPath);
            await sourceStream.CopyToAsync(targetStream, ct);
        }

        /// <summary>
        /// Safely deletes a directory and all its contents.
        /// </summary>
        /// <param name="directoryPath">Directory to delete.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public static void SafeDeleteDirectory(string directoryPath, ILogger? logger = null)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    logger?.LogDebug("Cleaning up directory: {DirectoryPath}", directoryPath);
                    Directory.Delete(directoryPath, recursive: true);
                    logger?.LogInformation("Successfully cleaned up directory");
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to clean up directory: {DirectoryPath}", directoryPath);
            }
        }
    }
}
