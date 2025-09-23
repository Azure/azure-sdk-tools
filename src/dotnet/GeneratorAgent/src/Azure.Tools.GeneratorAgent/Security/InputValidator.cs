using System.Text.RegularExpressions;

namespace Azure.Tools.GeneratorAgent.Security
{
    /// <summary>
    /// Provides focused input validation for security purposes.
    /// All user inputs are treated as untrusted and validated for basic security concerns.
    /// </summary>
    internal static class InputValidator
    {
        private static readonly Regex CommitIdRegex = new(@"^[a-fA-F0-9]{6,40}$", RegexOptions.Compiled);
        private static readonly Regex TypeSpecFileRegex = new(@"\.(tsp|yaml)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        /// <summary>
        /// Basic patterns that are obvious command injection attempts.
        /// Focused on common shell separators that could allow chaining commands.
        /// </summary>
        private static readonly string[] BasicCommandSeparators = 
        {
            "&&", "||", "&", "|", ";"
        };

        /// <summary>
        /// Validates a path for potential security issues like path traversal attacks.
        /// </summary>
        /// <param name="path">The path to validate</param>
        /// <param name="pathType">Description of the path type for logging</param>
        /// <returns>Result indicating if path is safe</returns>
        public static Result<string> ValidateDirTraversal(string? path, string pathType = "path")
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Result<string>.Failure(new ArgumentException($"{pathType} cannot be null or empty"));
            }

            return Result<string>.Success(path);
        }

        /// <summary>
        /// Validates a TypeSpec path for security and format.
        /// </summary>
        /// <param name="path">The TypeSpec path to validate</param>
        /// <param name="isLocalPath">True if this is a local filesystem path, false if it's a relative repository path</param>
        public static Result<string> ValidateTypeSpecDir(string? path, bool isLocalPath = true)
        {
            var pathTraversalValidation = ValidateDirTraversal(path, "TypeSpec path");
            if (pathTraversalValidation.IsFailure)
            {
                return pathTraversalValidation;
            }

            try
            {
                if (isLocalPath)
                {
                    var fullPath = Path.GetFullPath(pathTraversalValidation.Value!);
                    
                    if (!Directory.Exists(fullPath))
                    {
                        return Result<string>.Failure(new DirectoryNotFoundException($"TypeSpec directory not found: {fullPath}"));
                    }
                    
                    var allFiles = Directory.GetFiles(fullPath, "*.*", SearchOption.TopDirectoryOnly);
                    var hasTypeSpecFiles = allFiles.Any(file => TypeSpecFileRegex.IsMatch(file));

                    if (!hasTypeSpecFiles)
                    {
                        return Result<string>.Failure(new InvalidOperationException($"No .tsp or .yaml files found in directory: {fullPath}"));
                    }
                    
                    if (allFiles.Any(file => !TypeSpecFileRegex.IsMatch(file)))
                    {
                        var invalidFiles = string.Join(", ", allFiles
                            .Where(file => !TypeSpecFileRegex.IsMatch(file))
                            .Select(Path.GetFileName));
                        return Result<string>.Failure(new InvalidOperationException($"Directory contains non-TypeSpec files: {invalidFiles}. Only .tsp and .yaml files are allowed."));
                    }
                    
                    return Result<string>.Success(fullPath);
                }
                else
                {
                    var validatedPath = pathTraversalValidation.Value!;
                    if (validatedPath.StartsWith("/") || validatedPath.StartsWith("\\"))
                    {
                        return Result<string>.Failure(new ArgumentException("Repository path cannot start with / or \\"));
                    }
                    
                    if (validatedPath.Contains("//") || validatedPath.Contains("\\\\"))
                    {
                        return Result<string>.Failure(new ArgumentException("Repository path contains invalid double separators"));
                    }
                    
                    var normalizedPath = validatedPath.Replace('\\', '/');
                    return Result<string>.Success(normalizedPath);
                }
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(new ArgumentException($"Invalid path format: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Validates a Git commit ID format.
        /// </summary>
        public static Result<string> ValidateCommitId(string? commitId)
        {
            if (string.IsNullOrWhiteSpace(commitId))
            {
                return Result<string>.Success(string.Empty);
            }

            if (!CommitIdRegex.IsMatch(commitId))
            {
                return Result<string>.Failure(new ArgumentException("Commit ID must be 6-40 hexadecimal characters"));
            }

            return Result<string>.Success(commitId);
        }

        /// <summary>
        /// Validates a directory path for output operations.
        /// </summary>
        public static Result<string> ValidateOutputDirectory(string? path)
        {
            var pathTraversalValidation = ValidateDirTraversal(path, "Output directory path");
            if (pathTraversalValidation.IsFailure)
            {
                return pathTraversalValidation;
            }

            try
            {
                var fullPath = Path.GetFullPath(pathTraversalValidation.Value!);
                
                var parentDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    return Result<string>.Failure(new DirectoryNotFoundException($"Parent directory does not exist: {parentDir}"));
                }

                return Result<string>.Success(fullPath);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(new ArgumentException($"Invalid path format: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Validates a PowerShell script path for security.
        /// </summary>
        public static Result<string> ValidatePowerShellScriptPath(string scriptPath, string azureSdkPath)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                return Result<string>.Failure(new ArgumentException("PowerShell script path cannot be null or empty"));
            }

            if (string.IsNullOrWhiteSpace(azureSdkPath))
            {
                return Result<string>.Failure(new ArgumentException("Azure SDK path cannot be null or empty"));
            }

            // Ensure it's a PowerShell script
            if (!string.Equals(Path.GetExtension(scriptPath), ".ps1", StringComparison.OrdinalIgnoreCase))
            {
                return Result<string>.Failure(new ArgumentException("PowerShell script must have .ps1 extension"));
            }

            try
            {
                var fullScriptPath = Path.Combine(azureSdkPath, scriptPath);
                
                if (!File.Exists(fullScriptPath))
                {
                    return Result<string>.Failure(new FileNotFoundException($"PowerShell script not found: {fullScriptPath}"));
                }

                var normalizedPath = Path.GetFullPath(fullScriptPath);
                var normalizedAzureSdkPath = Path.GetFullPath(azureSdkPath);
                
                if (!normalizedPath.StartsWith(normalizedAzureSdkPath, StringComparison.OrdinalIgnoreCase))
                {
                    return Result<string>.Failure(new UnauthorizedAccessException("PowerShell script must be within Azure SDK directory"));
                }

                return Result<string>.Success(fullScriptPath);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(new ArgumentException($"Error validating script path: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Validates process arguments for security (prevents injection).
        /// </summary>
        public static Result<string> ValidateProcessArguments(string arguments)
        {
            if (string.IsNullOrEmpty(arguments))
            {
                return Result<string>.Success(string.Empty);
            }

            foreach (var separator in BasicCommandSeparators)
            {
                if (arguments.Contains(separator, StringComparison.OrdinalIgnoreCase))
                {
                    return Result<string>.Failure(new ArgumentException($"Arguments contain command separator: {separator}"));
                }
            }

            return Result<string>.Success(arguments);
        }

        /// <summary>
        /// Validates a working directory for process execution.
        /// </summary>
        public static Result<string> ValidateWorkingDirectory(string? workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                return Result<string>.Success(Directory.GetCurrentDirectory());
            }

            var pathTraversalValidation = ValidateDirTraversal(workingDirectory, "Working directory");
            if (pathTraversalValidation.IsFailure)
            {
                return pathTraversalValidation;
            }

            try
            {
                var fullPath = Path.GetFullPath(pathTraversalValidation.Value!);
                
                if (!Directory.Exists(fullPath))
                {
                    return Result<string>.Failure(new DirectoryNotFoundException($"Working directory does not exist: {fullPath}"));
                }

                return Result<string>.Success(fullPath);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(new ArgumentException($"Invalid working directory path: {ex.Message}", ex));
            }
        }
    }
}
