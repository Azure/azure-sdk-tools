using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Security;
using Microsoft.Extensions.Logging;
using System.Security;
using System.Text;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Service responsible for retrieving TypeSpec files from various sources (local or GitHub).
    /// </summary>
    internal class TypeSpecFileService : IDisposable
    {
        private readonly ILogger<TypeSpecFileService> Logger;
        private readonly GitHubFileService GitHubFileService;
        
        // Security: Track temp directories for proper cleanup
        private readonly List<string> TempDirectories = new();
        private bool IsDisposed;

        public TypeSpecFileService(
            ILogger<TypeSpecFileService> logger,
            GitHubFileService gitHubFileService)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(gitHubFileService);

            Logger = logger;
            GitHubFileService = gitHubFileService;
        }

        public async Task<Dictionary<string, string>> GetTypeSpecFilesAsync(ValidationContext validationContext, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ArgumentNullException.ThrowIfNull(validationContext);

            string? directoryToUse = validationContext.CurrentTypeSpecDir;

            if (string.IsNullOrWhiteSpace(directoryToUse))
            {
                throw new InvalidOperationException(
                    "No TypeSpec directory available. Call EnsureTypeSpecFilesAvailableAsync() first for GitHub scenarios.");
            }
            if (!Directory.Exists(directoryToUse))
            {
                throw new DirectoryNotFoundException($"TypeSpec directory not found: {directoryToUse}");
            }

            return await GetLocalTypeSpecFilesAsync(directoryToUse, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Dictionary<string, string>> GetLocalTypeSpecFilesAsync(
            string typeSpecDir, 
            CancellationToken cancellationToken)
        {
            var typeSpecFiles = new Dictionary<string, string>();
            string[] allFiles = Directory.GetFiles(typeSpecDir, "*.tsp", SearchOption.AllDirectories);

            try
            {
                foreach (string filePath in allFiles)
                {
                    string fileName = Path.GetFileName(filePath);
                    string content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                    typeSpecFiles[fileName] = content;
                }
                
                return typeSpecFiles;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to read TypeSpec files from directory '{typeSpecDir}': {ex.Message}", ex);
            }
        }

        public async Task DownloadGitHubTypeSpecFilesAsync(ValidationContext validationContext, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ArgumentNullException.ThrowIfNull(validationContext);

            Logger.LogInformation("Downloading TypeSpec files from GitHub repository");

            var githubFiles = await GitHubFileService.GetTypeSpecFilesAsync(
                validationContext.ValidatedCommitId,
                validationContext.ValidatedTypeSpecDir,
                cancellationToken).ConfigureAwait(false);
            
            if (githubFiles.Count == 0)
            {
                throw new InvalidOperationException("No TypeSpec files found in GitHub repository");
            }

            // Create secure temporary directory for compilation
            var tempDirectory = await WriteGithubFilesToTempDirectory(githubFiles, validationContext, cancellationToken).ConfigureAwait(false);
            validationContext.CurrentTypeSpecDirForCompilation = tempDirectory;

            Logger.LogInformation("Successfully downloaded {Count} TypeSpec files from GitHub and created temp directory: {TempDir}", 
                githubFiles.Count, tempDirectory);
        }

        /// <summary>
        /// Creates a secure temporary directory and writes GitHub files to it.
        /// Implements essential security measures for production use.
        /// </summary>
        private async Task<string> WriteGithubFilesToTempDirectory(
            Dictionary<string, string> githubFiles, 
            ValidationContext validationContext,
            CancellationToken cancellationToken)
        {
            // Create validated temp path
            var tempPath = CreateSecureTempPath(validationContext);

            // Validate the constructed path
            tempPath = InputValidator.ValidateDirTraversal(tempPath, "temporary TypeSpec directory");

            try
            {
                // Create directory
                Directory.CreateDirectory(tempPath);
                TempDirectories.Add(tempPath);

                // Write files with validation
                await WriteFilesSecurely(tempPath, githubFiles, cancellationToken).ConfigureAwait(false);

                Logger.LogDebug("Created secure temp directory: {TempPath} with {FileCount} files", tempPath, githubFiles.Count);
                return tempPath;
            }
            catch (Exception ex)
            {
                await CleanupTempDirectorySecurely(tempPath).ConfigureAwait(false);
                throw new InvalidOperationException($"Failed to create temporary directory '{tempPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a secure temporary directory path with proper validation.
        /// Path format: {ValidatedSdkDir}\temp\typespec\{sanitized-spec-dir}\{timestamp}_{uniqueId}
        /// </summary>
        private string CreateSecureTempPath(ValidationContext validationContext)
        {
            // Sanitize the TypeSpec directory name
            string sanitizedSpecDir = SanitizeDirectoryNameSecurely(validationContext.ValidatedTypeSpecDir);
            
            // Create unique timestamp + random identifier
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            string uniqueId = Path.GetRandomFileName()[..8]; // First 8 chars for uniqueness
            
            string tempPath = Path.Combine(
                validationContext.ValidatedSdkDir,
                "temp",
                "typespec",
                sanitizedSpecDir,
                $"{timestamp}_{uniqueId}");

            return tempPath;
        }

        /// <summary>
        /// Writes files securely with proper validation and error handling.
        /// </summary>
        private async Task WriteFilesSecurely(
            string tempPath, 
            Dictionary<string, string> githubFiles, 
            CancellationToken cancellationToken)
        {
            string fullTempPath = Path.GetFullPath(tempPath);

            foreach (var kvp in githubFiles)
            {
                try
                {
                    // Validate each filename
                    var validatedFileName = InputValidator.ValidateDirTraversal(kvp.Key, "TypeSpec filename");

                    string filePath = Path.Combine(tempPath, kvp.Key);
                    
                    // Ensure file path is within temp directory (prevent directory traversal)
                    string fullFilePath = Path.GetFullPath(filePath);
                    if (!fullFilePath.StartsWith(fullTempPath, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.LogWarning("Skipping file outside temp directory: {FileName}", kvp.Key);
                        continue;
                    }

                    // Create subdirectories safely
                    string? directoryPath = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    // Write file with proper encoding
                    await File.WriteAllTextAsync(filePath, kvp.Value, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                    
                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        Logger.LogDebug("Securely wrote file: {FilePath} ({Size} characters)", filePath, kvp.Value.Length);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("Skipping invalid filename {FileName}: {Error}", kvp.Key, ex.Message);
                    continue;
                }
            }
        }

        /// <summary>
        /// Sanitizes directory name for secure filesystem usage.
        /// </summary>
        private static string SanitizeDirectoryNameSecurely(string directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName))
                return "default";

            // Replace all invalid and potentially dangerous characters
            char[] invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new StringBuilder(directoryName.Length);

            foreach (char c in directoryName)
            {
                if (invalidChars.Contains(c) || c == '/' || c == '\\' || c == '.' || c == ' ')
                {
                    sanitized.Append('_');
                }
                else
                {
                    sanitized.Append(c);
                }
            }

            string result = sanitized.ToString();
            
            // Limit length for filesystem compatibility
            const int maxLength = 50; // Conservative length for cross-platform compatibility
            if (result.Length > maxLength)
            {
                result = result[..maxLength];
            }

            // Ensure not empty after sanitization
            return string.IsNullOrEmpty(result) ? "default" : result.TrimEnd('_');
        }

        /// <summary>
        /// Updates a TypeSpec file in the current working directory with security validation.
        /// Used for iterative error fixing.
        /// </summary>
        public async Task<bool> UpdateTypeSpecFileAsync(string fileName, string content, ValidationContext validationContext, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            ArgumentNullException.ThrowIfNull(content);
            ArgumentNullException.ThrowIfNull(validationContext);

            // Validate filename
            try
            {
                var validatedFileName = InputValidator.ValidateDirTraversal(fileName, "TypeSpec filename");
            }
            catch (Exception ex)
            {
               throw new SecurityException($"TypeSpec filename validation failed: {fileName}", ex);
            }

            string currentDir = validationContext.CurrentTypeSpecDir;
            string filePath = Path.Combine(currentDir, fileName);
            
            // Ensure file is within current directory
            string fullCurrentDir = Path.GetFullPath(currentDir);
            string fullFilePath = Path.GetFullPath(filePath);
            if (!fullFilePath.StartsWith(fullCurrentDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityException($"File '{fileName}' attempts to write outside current directory");
            }

            try
            {
                await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to write TypeSpec file '{fileName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Securely cleans up a temporary directory.
        /// </summary>
        private async Task CleanupTempDirectorySecurely(string tempDirectory)
        {
            if (string.IsNullOrEmpty(tempDirectory) || !Directory.Exists(tempDirectory))
                return;

            try
            {
                await Task.Run(() =>
                {
                    Directory.Delete(tempDirectory, recursive: true);
                    Logger.LogDebug("Securely cleaned up temp directory: {TempDir}", tempDirectory);
                });
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to cleanup temp directory: {TempDir}", tempDirectory);
            }
        }

        /// <summary>
        /// Securely disposes of resources and cleans up temporary directories.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed) return;

            try
            {
                // Clean up all tracked temp directories
                foreach (string tempDir in TempDirectories)
                {
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, recursive: true);
                            if (Logger.IsEnabled(LogLevel.Debug))
                            {
                                Logger.LogDebug("Disposed temp directory: {TempDir}", tempDir);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to dispose temp directory: {TempDir}", tempDir);
                    }
                }

                TempDirectories.Clear();
            }
            finally
            {
                IsDisposed = true;
            }
        }
    }
}
