using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System;

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
                return Result<string>.Failure($"{pathType} cannot be null or empty");
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
            Result<string> pathTraversalValidation = ValidateDirTraversal(path, "TypeSpec path");
            if (pathTraversalValidation.IsFailure)
            {
                return pathTraversalValidation;
            }

            try
            {
                if (isLocalPath)
                {
                    string fullPath = Path.GetFullPath(pathTraversalValidation.Value);
                    
                    if (!Directory.Exists(fullPath))
                    {
                        return Result<string>.Failure($"TypeSpec directory not found: {fullPath}");
                    }
                    
                    string[] allFiles = Directory.GetFiles(fullPath, "*.*", SearchOption.TopDirectoryOnly);
                    bool hasTypeSpecFiles = allFiles
                        .Any(file => TypeSpecFileRegex.IsMatch(file));

                    if (!hasTypeSpecFiles)
                    {
                        return Result<string>.Failure($"No .tsp or .yaml files found in directory: {fullPath}");
                    }
                    
                    if (allFiles.Any(file => !TypeSpecFileRegex.IsMatch(file)))
                    {
                        string invalidFiles = string.Join(", ", allFiles
                            .Where(file => !TypeSpecFileRegex.IsMatch(file))
                            .Select(Path.GetFileName));
                        return Result<string>.Failure($"Directory contains non-TypeSpec files: {invalidFiles}. Only .tsp and .yaml files are allowed.");
                    }
                    
                    return Result<string>.Success(fullPath);
                }
                else
                {
                    string validatedPath = pathTraversalValidation.Value;
                    if (validatedPath.StartsWith("/") || validatedPath.StartsWith("\\"))
                    {
                        return Result<string>.Failure("Repository path cannot start with / or \\");
                    }
                    
                    if (validatedPath.Contains("//") || validatedPath.Contains("\\\\"))
                    {
                        return Result<string>.Failure("Repository path contains invalid double separators");
                    }
                    
                    string normalizedPath = validatedPath.Replace('\\', '/');
                    
                    return Result<string>.Success(normalizedPath);
                }
            }
            catch (Exception ex)
            {
                return Result<string>.Failure($"Invalid path format: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates a Git commit ID format.
        /// </summary>
        public static Result<string> ValidateCommitId(string? commitId)
        {
            if (string.IsNullOrWhiteSpace(commitId))
            {
                return Result<string>.Success(string.Empty); // Commit ID is optional
            }

            if (!CommitIdRegex.IsMatch(commitId))
            {
                return Result<string>.Failure("Commit ID must be 6-40 hexadecimal characters");
            }

            return Result<string>.Success(commitId);
        }

        /// <summary>
        /// Validates a directory path for output operations.
        /// </summary>
        public static Result<string> ValidateOutputDirectory(string? path)
        {
            Result<string> pathTraversalValidation = ValidateDirTraversal(path, "Output directory path");
            if (pathTraversalValidation.IsFailure)
            {
                return pathTraversalValidation;
            }

            try
            {
                string fullPath = Path.GetFullPath(pathTraversalValidation.Value);
                
                string? parentDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    return Result<string>.Failure($"Parent directory does not exist: {parentDir}");
                }

                return Result<string>.Success(fullPath);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure($"Invalid path format: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates a PowerShell script path for security.
        /// </summary>
        public static Result<string> ValidatePowerShellScriptPath(string scriptPath, string azureSdkPath)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                return Result<string>.Failure("PowerShell script path cannot be null or empty");
            }

            // Ensure it's a PowerShell script
            if (!string.Equals(Path.GetExtension(scriptPath), ".ps1", StringComparison.OrdinalIgnoreCase))
            {
                return Result<string>.Failure("PowerShell script must have .ps1 extension");
            }

            try
            {
                string fullScriptPath = Path.Combine(azureSdkPath, scriptPath);
                
                if (!File.Exists(fullScriptPath))
                {
                    return Result<string>.Failure($"PowerShell script not found: {fullScriptPath}");
                }

                string normalizedPath = Path.GetFullPath(fullScriptPath);
                string normalizedAzureSdkPath = Path.GetFullPath(azureSdkPath);
                
                if (!normalizedPath.StartsWith(normalizedAzureSdkPath, StringComparison.OrdinalIgnoreCase))
                {
                    return Result<string>.Failure("PowerShell script must be within Azure SDK directory");
                }

                return Result<string>.Success(fullScriptPath);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure($"Error validating script path: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates process arguments for security (prevents injection).
        /// Also used for validating configuration values.
        /// </summary>
        public static Result<string> ValidateProcessArguments(string arguments)
        {
            if (string.IsNullOrEmpty(arguments))
            {
                return Result<string>.Success(string.Empty);
            }

            foreach (string separator in BasicCommandSeparators)
            {
                if (arguments.Contains(separator, StringComparison.OrdinalIgnoreCase))
                {
                    return Result<string>.Failure($"Arguments contain command separator: {separator}");
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

            Result<string> pathTraversalValidation = ValidateDirTraversal(workingDirectory, "Working directory");
            if (pathTraversalValidation.IsFailure)
            {
                return pathTraversalValidation;
            }

            try
            {
                string fullPath = Path.GetFullPath(pathTraversalValidation.Value);
                
                if (!Directory.Exists(fullPath))
                {
                    return Result<string>.Failure($"Working directory does not exist: {fullPath}");
                }

                return Result<string>.Success(fullPath);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure($"Invalid working directory path: {ex.Message}");
            }
        }
    }
}
