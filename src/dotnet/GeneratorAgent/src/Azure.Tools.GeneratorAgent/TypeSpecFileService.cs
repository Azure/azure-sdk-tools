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
        private readonly ValidationContext ValidationContext;
        private readonly Func<ValidationContext, GitHubFileService> GitHubServiceFactory;
        private GitHubFileService? GitHubFileService;
        
        // Security: Track temp directories for proper cleanup
        private readonly List<string> TempDirectories = new();
        private bool IsDisposed;

        public TypeSpecFileService(
            ILogger<TypeSpecFileService> logger,
            ValidationContext validationContext,
            Func<ValidationContext, GitHubFileService> gitHubServiceFactory)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(validationContext);
            ArgumentNullException.ThrowIfNull(gitHubServiceFactory);

            Logger = logger;
            ValidationContext = validationContext;
            GitHubServiceFactory = gitHubServiceFactory;
        }

        /// <summary>
        /// Gets TypeSpec files from either local directory or GitHub repository.
        /// Returns a dictionary where key is filename and value is file content.
        /// </summary>
        public async Task<Dictionary<string, string>> GetTypeSpecFilesAsync(
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            if (string.IsNullOrWhiteSpace(ValidationContext.ValidatedCommitId))
            {
                // Local case
                return await GetLocalTypeSpecFilesAsync(ValidationContext.ValidatedTypeSpecDir, cancellationToken);
            }
            else
            {
                // GitHub case
                return await GetGitHubTypeSpecFilesAsync(cancellationToken);
            }
        }

        private async Task<Dictionary<string, string>> GetLocalTypeSpecFilesAsync(
            string typeSpecDir, 
            CancellationToken cancellationToken)
        {
            Logger.LogInformation("Reading TypeSpec files from local directory: {TypeSpecDir}", typeSpecDir);

            try
            {
                Dictionary<string, string> typeSpecFiles = new Dictionary<string, string>();
                
                string[] allFiles = Directory.GetFiles(typeSpecDir, "*.tsp", SearchOption.AllDirectories);

                foreach (string filePath in allFiles)
                {
                    string fileName = Path.GetFileName(filePath);
                    string content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
                    typeSpecFiles[fileName] = content;
                }

                Logger.LogInformation("Successfully read {Count} TypeSpec files from local directory\n", typeSpecFiles.Count);
                return typeSpecFiles;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogCritical(ex, "Error reading TypeSpec files from local directory: {TypeSpecDir}", typeSpecDir);
                throw;
            }
        }

        private async Task<Dictionary<string, string>> GetGitHubTypeSpecFilesAsync(
            CancellationToken cancellationToken)
        {
            Logger.LogInformation("Fetching TypeSpec files from GitHub: {TypeSpecDir} at commit {CommitId}", 
                ValidationContext.ValidatedTypeSpecDir, ValidationContext.ValidatedCommitId);

            try
            {
                GitHubFileService = GitHubServiceFactory(ValidationContext);

                // Get files from GitHub
                Dictionary<string, string> result = await GitHubFileService.GetTypeSpecFilesAsync(cancellationToken);

                // Create secure temporary directory for compilation
                if (result.Count > 0)
                {
                    string tempDirectory = await CreateSecureTempDirectoryFromGitHubFiles(result, cancellationToken);
                    ValidationContext.UpdateTypeSpecDirForCompilation(tempDirectory);

                    Logger.LogInformation("Successfully read {Count} TypeSpec files from Github Repository\n", result.Count);
                }

                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogCritical(ex, "Unexpected error fetching TypeSpec files from GitHub: {TypeSpecDir} at commit {CommitId}", 
                    ValidationContext.ValidatedTypeSpecDir, ValidationContext.ValidatedCommitId);
                throw;
            }
        }

        /// <summary>
        /// Creates a secure temporary directory and writes GitHub files to it.
        /// Implements essential security measures for production use.
        /// </summary>
        private async Task<string> CreateSecureTempDirectoryFromGitHubFiles(
            Dictionary<string, string> githubFiles, 
            CancellationToken cancellationToken)
        {
            string tempPath = string.Empty;

            try
            {
                // Security: Create validated temp path
                tempPath = CreateSecureTempPath();

                // Security: Validate the constructed path
                Result<string> pathValidation = InputValidator.ValidateDirTraversal(tempPath, "temporary TypeSpec directory");
                if (pathValidation.IsFailure)
                {
                    throw new SecurityException($"Temporary directory path validation failed: {pathValidation.Exception?.Message}");
                }

                // Create directory
                Directory.CreateDirectory(tempPath);
                TempDirectories.Add(tempPath);

                // Security: Write files with validation
                await WriteFilesSecurely(tempPath, githubFiles, cancellationToken);

                Logger.LogInformation("Created secure temp directory: {TempPath} with {FileCount} files", tempPath, githubFiles.Count);
                return tempPath;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Security: Always cleanup on failure
                await CleanupTempDirectorySecurely(tempPath);
                
                Logger.LogError(ex, "Failed to create secure temp directory: {TempPath}", tempPath);
                throw new InvalidOperationException($"Failed to create secure temporary TypeSpec directory: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a secure temporary directory path with proper validation.
        /// Path format: {ValidatedSdkDir}\temp\typespec\{sanitized-spec-dir}\{timestamp}_{uniqueId}
        /// </summary>
        private string CreateSecureTempPath()
        {
            // Security: Sanitize the TypeSpec directory name
            string sanitizedSpecDir = SanitizeDirectoryNameSecurely(ValidationContext.ValidatedTypeSpecDir);
            
            // Security: Create unique timestamp + random identifier
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            string uniqueId = Path.GetRandomFileName()[..8]; // First 8 chars for uniqueness
            
            string tempPath = Path.Combine(
                ValidationContext.ValidatedSdkDir,
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
                // Security: Validate each filename
                Result<string> fileValidation = InputValidator.ValidateDirTraversal(kvp.Key, "TypeSpec filename");
                if (fileValidation.IsFailure)
                {
                    Logger.LogWarning("Skipping invalid filename: {FileName}", kvp.Key);
                    continue;
                }

                string filePath = Path.Combine(tempPath, kvp.Key);
                
                // Security: Ensure file path is within temp directory (prevent directory traversal)
                string fullFilePath = Path.GetFullPath(filePath);
                if (!fullFilePath.StartsWith(fullTempPath, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning("Skipping file outside temp directory: {FileName}", kvp.Key);
                    continue;
                }

                // Security: Create subdirectories safely
                string? directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // Write file with proper encoding
                await File.WriteAllTextAsync(filePath, kvp.Value, Encoding.UTF8, cancellationToken);
                Logger.LogDebug("Securely wrote file: {FilePath} ({Size} characters)", filePath, kvp.Value.Length);
            }
        }

        /// <summary>
        /// Sanitizes directory name for secure filesystem usage.
        /// </summary>
        private static string SanitizeDirectoryNameSecurely(string directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName))
                return "default";

            // Security: Replace all invalid and potentially dangerous characters
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
            
            // Security: Limit length for filesystem compatibility
            const int maxLength = 50; // Conservative length for cross-platform compatibility
            if (result.Length > maxLength)
            {
                result = result[..maxLength];
            }

            // Security: Ensure not empty after sanitization
            return string.IsNullOrEmpty(result) ? "default" : result.TrimEnd('_');
        }

        /// <summary>
        /// Updates a TypeSpec file in the current working directory with security validation.
        /// Used for iterative error fixing.
        /// </summary>
        public async Task UpdateTypeSpecFileAsync(string fileName, string content, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            ArgumentNullException.ThrowIfNull(content);

            // Security: Validate filename
            Result<string> fileValidation = InputValidator.ValidateDirTraversal(fileName, "TypeSpec filename");
            if (fileValidation.IsFailure)
            {
                throw new SecurityException($"TypeSpec filename validation failed: {fileName}");
            }

            string currentDir = ValidationContext.GetCurrentTypeSpecDir();
            string filePath = Path.Combine(currentDir, fileName);
            
            // Security: Ensure file is within current directory
            string fullCurrentDir = Path.GetFullPath(currentDir);
            string fullFilePath = Path.GetFullPath(filePath);
            if (!fullFilePath.StartsWith(fullCurrentDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityException($"File '{fileName}' attempts to write outside current directory");
            }

            try
            {
                await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken);
                Logger.LogInformation("Securely updated TypeSpec file: {FilePath} ({Size} characters)", filePath, content.Length);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not SecurityException)
            {
                Logger.LogError(ex, "Failed to update TypeSpec file: {FilePath}", filePath);
                throw new InvalidOperationException($"Failed to update TypeSpec file: {fileName}", ex);
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
                // Security: Use Task.Run for potentially blocking I/O operation
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
                // Security: Clean up all tracked temp directories
                foreach (string tempDir in TempDirectories)
                {
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, recursive: true);
                            Logger.LogDebug("Disposed temp directory: {TempDir}", tempDir);
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
