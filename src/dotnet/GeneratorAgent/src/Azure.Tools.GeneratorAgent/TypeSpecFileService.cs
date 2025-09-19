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
        /// Returns a Result containing dictionary where key is filename and value is file content.
        /// </summary>
        public async Task<Result<Dictionary<string, string>>> GetTypeSpecFilesAsync(
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            Result<Dictionary<string, string>> result;

            if (string.IsNullOrWhiteSpace(ValidationContext.ValidatedCommitId))
            {
                // Local case
                result = await GetLocalTypeSpecFilesAsync(ValidationContext.ValidatedTypeSpecDir, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // GitHub case
                result = await GetGitHubTypeSpecFilesAsync(cancellationToken).ConfigureAwait(false);
            }

            // Handle result and log appropriately
            if (result.IsFailure)
            {
                Logger.LogError("Failed to load TypeSpec files: {Error}", result.Exception?.Message);
                return result;
            }
            
            var typeSpecFiles = result.Value!;
            if (typeSpecFiles.Count == 0)
            {
                Logger.LogWarning("No TypeSpec files found");
            }

            return result;
        }

        private async Task<Result<Dictionary<string, string>>> GetLocalTypeSpecFilesAsync(
            string typeSpecDir, 
            CancellationToken cancellationToken)
        {
            // Fail fast validation - check directory exists
            if (!Directory.Exists(typeSpecDir))
            {
                return Result<Dictionary<string, string>>.Failure(
                    new DirectoryNotFoundException($"TypeSpec directory not found: {typeSpecDir}"));
            }

            var typeSpecFiles = new Dictionary<string, string>();
            string[] allFiles = Directory.GetFiles(typeSpecDir, "*.tsp", SearchOption.AllDirectories);

            try
            {
                foreach (string filePath in allFiles)
                {
                    string fileName = Path.GetFileName(filePath);
                    string content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                    typeSpecFiles[fileName] = content;
                    
                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        Logger.LogDebug("Loaded file: {FileName} ({Size} characters)", fileName, content.Length);
                    }
                }
                
                return Result<Dictionary<string, string>>.Success(typeSpecFiles);
            }
            catch (Exception ex)
            {
                return Result<Dictionary<string, string>>.Failure(new InvalidOperationException($"Failed to read TypeSpec files from directory '{typeSpecDir}': {ex.Message}", ex));
            }
        }

        private async Task<Result<Dictionary<string, string>>> GetGitHubTypeSpecFilesAsync(
            CancellationToken cancellationToken)
        {
            GitHubFileService = GitHubServiceFactory(ValidationContext);

            var githubResult = await GitHubFileService.GetTypeSpecFilesAsync(cancellationToken).ConfigureAwait(false);
            
            if (githubResult.IsFailure)
            {
                return Result<Dictionary<string, string>>.Failure(githubResult.Exception!);
            }

            var result = githubResult.Value ?? new Dictionary<string, string>();

            if (result.Count > 0)
            {
                var tempDirResult = await CreateSecureTempDirectoryFromGitHubFiles(result, cancellationToken).ConfigureAwait(false);
                if (tempDirResult.IsFailure)
                {
                    return Result<Dictionary<string, string>>.Failure(tempDirResult.Exception!);
                }
                
                ValidationContext.CurrentTypeSpecDirForCompilation = tempDirResult.Value!;
            }

            return Result<Dictionary<string, string>>.Success(result);
        }

        /// <summary>
        /// Creates a secure temporary directory and writes GitHub files to it.
        /// Implements essential security measures for production use.
        /// </summary>
        private async Task<Result<string>> CreateSecureTempDirectoryFromGitHubFiles(
            Dictionary<string, string> githubFiles, 
            CancellationToken cancellationToken)
        {
            string tempPath = string.Empty;

            // Create validated temp path
            tempPath = CreateSecureTempPath();

            // Validate the constructed path
            Result<string> pathValidation = InputValidator.ValidateDirTraversal(tempPath, "temporary TypeSpec directory");
            if (pathValidation.IsFailure)
            {
                return Result<string>.Failure(new SecurityException($"Temporary directory path validation failed: {pathValidation.Exception?.Message}"));
            }

            try
            {
                // Create directory
                Directory.CreateDirectory(tempPath);
                TempDirectories.Add(tempPath);

                // Write files with validation
                var writeResult = await WriteFilesSecurely(tempPath, githubFiles, cancellationToken).ConfigureAwait(false);
                if (writeResult.IsFailure)
                {
                    await CleanupTempDirectorySecurely(tempPath).ConfigureAwait(false);
                    return Result<string>.Failure(writeResult.Exception!);
                }

                Logger.LogDebug("Created secure temp directory: {TempPath} with {FileCount} files", tempPath, githubFiles.Count);
                return Result<string>.Success(tempPath);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(new InvalidOperationException($"Failed to create temporary directory '{tempPath}': {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Creates a secure temporary directory path with proper validation.
        /// Path format: {ValidatedSdkDir}\temp\typespec\{sanitized-spec-dir}\{timestamp}_{uniqueId}
        /// </summary>
        private string CreateSecureTempPath()
        {
            // Sanitize the TypeSpec directory name
            string sanitizedSpecDir = SanitizeDirectoryNameSecurely(ValidationContext.ValidatedTypeSpecDir);
            
            // Create unique timestamp + random identifier
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
        private async Task<Result<bool>> WriteFilesSecurely(
            string tempPath, 
            Dictionary<string, string> githubFiles, 
            CancellationToken cancellationToken)
        {
            string fullTempPath = Path.GetFullPath(tempPath);

            try
            {
                foreach (var kvp in githubFiles)
                {
                    // Validate each filename
                    Result<string> fileValidation = InputValidator.ValidateDirTraversal(kvp.Key, "TypeSpec filename");
                    if (fileValidation.IsFailure)
                    {
                        Logger.LogWarning("Skipping invalid filename: {FileName}", kvp.Key);
                        continue;
                    }

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

                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Failure(new InvalidOperationException($"Failed to write files to temporary directory '{tempPath}': {ex.Message}", ex));
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
        public async Task<Result<bool>> UpdateTypeSpecFileAsync(string fileName, string content, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            ArgumentNullException.ThrowIfNull(content);

            // Validate filename
            Result<string> fileValidation = InputValidator.ValidateDirTraversal(fileName, "TypeSpec filename");
            if (fileValidation.IsFailure)
            {
                return Result<bool>.Failure(new SecurityException($"TypeSpec filename validation failed: {fileName}"));
            }

            string currentDir = ValidationContext.CurrentTypeSpecDir;
            string filePath = Path.Combine(currentDir, fileName);
            
            // Ensure file is within current directory
            string fullCurrentDir = Path.GetFullPath(currentDir);
            string fullFilePath = Path.GetFullPath(filePath);
            if (!fullFilePath.StartsWith(fullCurrentDir, StringComparison.OrdinalIgnoreCase))
            {
                return Result<bool>.Failure(new SecurityException($"File '{fileName}' attempts to write outside current directory"));
            }

            try
            {
                await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Failure(new InvalidOperationException($"Failed to write TypeSpec file '{fileName}': {ex.Message}", ex));
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
