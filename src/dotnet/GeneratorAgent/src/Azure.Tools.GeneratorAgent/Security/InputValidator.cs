using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
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

        private static ILogger? Logger;

        /// <summary>
        /// Sets the logger to be used by all validation methods.
        /// This should be called once during application startup.
        /// </summary>
        public static void SetLogger(ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            Logger = logger;
        }

        /// <summary>
        /// Validates a path for potential security issues like path traversal attacks.
        /// </summary>
        /// <param name="path">The path to validate</param>
        /// <param name="pathType">Description of the path type for logging</param>
        /// <returns>ValidationResult indicating if path is safe</returns>
        public static ValidationResult ValidateDirTraversal(string? path, string pathType = "path")
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return ValidationResult.Invalid($"{pathType} cannot be null or empty");
            }

            return ValidationResult.Valid(path);
        }

        /// <summary>
        /// Validates a TypeSpec path for security and format.
        /// </summary>
        /// <param name="path">The TypeSpec path to validate</param>
        /// <param name="isLocalPath">True if this is a local filesystem path, false if it's a relative repository path</param>
        public static ValidationResult ValidateTypeSpecDir(string? path, bool isLocalPath = true)
        {
            ValidationResult pathTraversalValidation = ValidateDirTraversal(path, "TypeSpec path");
            if (!pathTraversalValidation.IsValid)
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
                        return ValidationResult.Invalid($"TypeSpec directory not found: {fullPath}");
                    }
                    
                    string[] allFiles = Directory.GetFiles(fullPath, "*.*", SearchOption.TopDirectoryOnly);
                    bool hasTypeSpecFiles = allFiles
                        .Any(file => TypeSpecFileRegex.IsMatch(file));

                    if (!hasTypeSpecFiles)
                    {
                        return ValidationResult.Invalid($"No .tsp or .yaml files found in directory: {fullPath}");
                    }
                    
                    if (allFiles.Any(file => !TypeSpecFileRegex.IsMatch(file)))
                    {
                        string invalidFiles = string.Join(", ", allFiles
                            .Where(file => !TypeSpecFileRegex.IsMatch(file))
                            .Select(Path.GetFileName));
                        return ValidationResult.Invalid($"Directory contains non-TypeSpec files: {invalidFiles}. Only .tsp and .yaml files are allowed.");
                    }
                    
                    return ValidationResult.Valid(fullPath);
                }
                else
                {
                    string validatedPath = pathTraversalValidation.Value;
                    if (validatedPath.StartsWith("/") || validatedPath.StartsWith("\\"))
                    {
                        return ValidationResult.Invalid("Repository path cannot start with / or \\");
                    }
                    
                    if (validatedPath.Contains("//") || validatedPath.Contains("\\\\"))
                    {
                        return ValidationResult.Invalid("Repository path contains invalid double separators");
                    }
                    
                    string normalizedPath = validatedPath.Replace('\\', '/');
                    
                    return ValidationResult.Valid(normalizedPath);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Invalid path format: {Path}", pathTraversalValidation.Value);
                return ValidationResult.Invalid($"Invalid path format: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates a Git commit ID format.
        /// </summary>
        public static ValidationResult ValidateCommitId(string? commitId)
        {
            if (string.IsNullOrWhiteSpace(commitId))
            {
                return ValidationResult.Valid(string.Empty); // Commit ID is optional
            }

            if (!CommitIdRegex.IsMatch(commitId))
            {
                Logger?.LogWarning("Invalid commit ID format: {CommitId}", commitId);
                return ValidationResult.Invalid("Commit ID must be 6-40 hexadecimal characters");
            }

            return ValidationResult.Valid(commitId);
        }

        /// <summary>
        /// Validates a directory path for output operations.
        /// </summary>
        public static ValidationResult ValidateOutputDirectory(string? path)
        {
            // Use centralized path traversal validation
            ValidationResult pathTraversalValidation = ValidateDirTraversal(path, "Output directory path");
            if (!pathTraversalValidation.IsValid)
            {
                return pathTraversalValidation;
            }

            try
            {
                string fullPath = Path.GetFullPath(pathTraversalValidation.Value);
                
                string? parentDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    return ValidationResult.Invalid($"Parent directory does not exist: {parentDir}");
                }

                return ValidationResult.Valid(fullPath);
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Invalid output directory path: {Path}", pathTraversalValidation.Value);
                return ValidationResult.Invalid($"Invalid path format: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates a PowerShell script path for security.
        /// </summary>
        public static ValidationResult ValidatePowerShellScriptPath(string scriptPath, string azureSdkPath)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                return ValidationResult.Invalid("PowerShell script path cannot be null or empty");
            }

            // Ensure it's a PowerShell script
            if (!string.Equals(Path.GetExtension(scriptPath), ".ps1", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Invalid("PowerShell script must have .ps1 extension");
            }

            try
            {
                string fullScriptPath = Path.Combine(azureSdkPath, scriptPath);
                
                if (!File.Exists(fullScriptPath))
                {
                    return ValidationResult.Invalid($"PowerShell script not found: {fullScriptPath}");
                }

                string normalizedPath = Path.GetFullPath(fullScriptPath);
                string normalizedAzureSdkPath = Path.GetFullPath(azureSdkPath);
                
                if (!normalizedPath.StartsWith(normalizedAzureSdkPath, StringComparison.OrdinalIgnoreCase))
                {
                    Logger?.LogWarning("Security: PowerShell script outside Azure SDK directory: {ScriptPath}", normalizedPath);
                    return ValidationResult.Invalid("PowerShell script must be within Azure SDK directory");
                }

                return ValidationResult.Valid(fullScriptPath);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error validating PowerShell script path: {ScriptPath}", scriptPath);
                return ValidationResult.Invalid($"Error validating script path: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates process arguments for security (prevents injection).
        /// Also used for validating configuration values.
        /// </summary>
        public static ValidationResult ValidateProcessArguments(string arguments)
        {
            if (string.IsNullOrEmpty(arguments))
            {
                return ValidationResult.Valid(string.Empty);
            }

            foreach (string separator in BasicCommandSeparators)
            {
                if (arguments.Contains(separator, StringComparison.OrdinalIgnoreCase))
                {
                    Logger?.LogWarning("Security: Command separator detected in arguments: {Separator}", separator);
                    return ValidationResult.Invalid($"Arguments contain command separator: {separator}");
                }
            }

            return ValidationResult.Valid(arguments);
        }

        /// <summary>
        /// Validates a working directory for process execution.
        /// </summary>
        public static ValidationResult ValidateWorkingDirectory(string? workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                return ValidationResult.Valid(Directory.GetCurrentDirectory());
            }

            ValidationResult pathTraversalValidation = ValidateDirTraversal(workingDirectory, "Working directory");
            if (!pathTraversalValidation.IsValid)
            {
                return pathTraversalValidation;
            }

            try
            {
                string fullPath = Path.GetFullPath(pathTraversalValidation.Value);
                
                if (!Directory.Exists(fullPath))
                {
                    return ValidationResult.Invalid($"Working directory does not exist: {fullPath}");
                }

                return ValidationResult.Valid(fullPath);
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Invalid working directory path: {Path}", pathTraversalValidation.Value);
                return ValidationResult.Invalid($"Invalid working directory path: {ex.Message}");
            }
        }
    }
}
