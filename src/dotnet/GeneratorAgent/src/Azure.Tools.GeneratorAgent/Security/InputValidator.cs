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
        public static string ValidateDirTraversal(string? path, string pathType = "path")
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException($"{pathType} cannot be null or empty");
            }

            return path;
        }

        /// <summary>
        /// Validates a TypeSpec path for security and format.
        /// </summary>
        /// <param name="path">The TypeSpec path to validate</param>
        /// <param name="isLocalPath">True if this is a local filesystem path, false if it's a relative repository path</param>
        public static string ValidateandNormalizeTypeSpecDir(string? path, bool isLocalPath = true)
        {
            var pathTraversalValidation = ValidateDirTraversal(path, "TypeSpec path");

            if (isLocalPath)
            {
                var fullPath = Path.GetFullPath(pathTraversalValidation);
                
                if (!Directory.Exists(fullPath))
                {
                    throw new DirectoryNotFoundException($"TypeSpec directory not found: {fullPath}");
                }
                
                var allFiles = Directory.GetFiles(fullPath, "*.*", SearchOption.TopDirectoryOnly);
                var hasTypeSpecFiles = allFiles.Any(file => TypeSpecFileRegex.IsMatch(file));

                if (!hasTypeSpecFiles)
                {
                    throw new InvalidOperationException($"No .tsp or .yaml files found in directory: {fullPath}");
                }
                
                if (allFiles.Any(file => !TypeSpecFileRegex.IsMatch(file)))
                {
                    var invalidFiles = string.Join(", ", allFiles
                        .Where(file => !TypeSpecFileRegex.IsMatch(file))
                        .Select(Path.GetFileName));
                    throw new InvalidOperationException($"Directory contains non-TypeSpec files: {invalidFiles}. Only .tsp and .yaml files are allowed.");
                }
                
                return fullPath;
            }
            else
            {
                var validatedPath = pathTraversalValidation;
                if (validatedPath.StartsWith("/") || validatedPath.StartsWith("\\"))
                {
                    throw new ArgumentException("Repository path cannot start with / or \\");
                }
                
                if (validatedPath.Contains("//") || validatedPath.Contains("\\\\"))
                {
                    throw new ArgumentException("Repository path contains invalid double separators");
                }
                
                var normalizedPath = validatedPath.Replace('\\', '/');
                return normalizedPath;
            }
        }

        /// <summary>
        /// Validates a Git commit ID format.
        /// </summary>
        public static string ValidateCommitId(string? commitId)
        {
            if (string.IsNullOrWhiteSpace(commitId))
            {
                return string.Empty;
            }

            if (!CommitIdRegex.IsMatch(commitId))
            {
                throw new ArgumentException("Commit ID must be 6-40 hexadecimal characters");
            }

            return commitId;
        }

        /// <summary>
        /// Validates a directory path for output operations.
        /// </summary>
        public static string ValidateOutputDirectory(string? path)
        {
            var pathTraversalValidation = ValidateDirTraversal(path, "Output directory path");

            var fullPath = Path.GetFullPath(pathTraversalValidation);
            
            var parentDir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                throw new DirectoryNotFoundException($"Parent directory does not exist: {parentDir}");
            }

            return fullPath;
        }

        /// <summary>
        /// Validates a PowerShell script path for security.
        /// </summary>
        public static string ValidatePowerShellScriptPath(string scriptPath, string azureSdkPath)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                throw new ArgumentException("PowerShell script path cannot be null or empty");
            }

            if (string.IsNullOrWhiteSpace(azureSdkPath))
            {
                throw new ArgumentException("Azure SDK path cannot be null or empty");
            }

            // Ensure it's a PowerShell script
            if (!string.Equals(Path.GetExtension(scriptPath), ".ps1", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("PowerShell script must have .ps1 extension");
            }

            var fullScriptPath = Path.Combine(azureSdkPath, scriptPath);
            
            if (!File.Exists(fullScriptPath))
            {
                throw new FileNotFoundException($"PowerShell script not found: {fullScriptPath}");
            }

            var normalizedPath = Path.GetFullPath(fullScriptPath);
            var normalizedAzureSdkPath = Path.GetFullPath(azureSdkPath);
            
            if (!normalizedPath.StartsWith(normalizedAzureSdkPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("PowerShell script must be within Azure SDK directory");
            }

            return fullScriptPath;
        }

        /// <summary>
        /// Validates process arguments for security (prevents injection).
        /// </summary>
        public static string ValidateProcessArguments(string arguments)
        {
            if (string.IsNullOrEmpty(arguments))
            {
                return string.Empty;
            }

            foreach (var separator in BasicCommandSeparators)
            {
                if (arguments.Contains(separator, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Arguments contain command separator: {separator}");
                }
            }

            return arguments;
        }

        /// <summary>
        /// Validates a working directory for process execution.
        /// Throws exceptions if validation fails.
        /// </summary>
        public static string ValidateWorkingDirectory(string? workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                return Directory.GetCurrentDirectory();
            }

            var validatedPath = ValidateDirTraversal(workingDirectory, "Working directory");

            try
            {
                var fullPath = Path.GetFullPath(validatedPath);

                if (!Directory.Exists(fullPath))
                {
                    throw new DirectoryNotFoundException($"Working directory does not exist: {fullPath}");
                }

                return fullPath;
            }
            catch (Exception ex) when (!(ex is DirectoryNotFoundException))
            {
                throw new ArgumentException($"Invalid working directory path: {ex.Message}", ex);
            }
        }
    }
}
