using System;
using System.IO;
using System.Threading;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Security;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// SDK generation service for TypeSpec projects from GitHub repositories.
    /// </summary>
    internal class GitHubTypeSpecSdkGenerationService : ISdkGenerationService
    {
        private readonly ILogger<GitHubTypeSpecSdkGenerationService> Logger;
        private readonly ProcessExecutor ProcessExecutor;
        private readonly AppSettings AppSettings;
        private readonly string CommitId;
        private readonly string TypespecSpecDir;
        private readonly string SdkOutputDir;

        public GitHubTypeSpecSdkGenerationService(
            AppSettings appSettings,
            ILogger<GitHubTypeSpecSdkGenerationService> logger,
            ProcessExecutor processExecutor,
            ValidationContext validationContext)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(processExecutor);
            ArgumentNullException.ThrowIfNull(validationContext);

            AppSettings = appSettings;
            Logger = logger;
            ProcessExecutor = processExecutor;

            CommitId = validationContext.ValidatedCommitId;
            TypespecSpecDir = validationContext.ValidatedTypeSpecDir;
            SdkOutputDir = validationContext.ValidatedSdkDir;
        }

        public async Task CompileTypeSpecAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Starting GitHub-based TypeSpec compilation for commit: {CommitId}", CommitId);
            Logger.LogInformation("SDK output directory: {SdkOutputDirectory}", SdkOutputDir);
            Logger.LogInformation("TypeSpec spec directory: {TypeSpecSpecDirectory}", TypespecSpecDir);

            string azureSdkDir = await ExtractAzureSdkDirAsync();

            await RunPowerShellGenerationScriptThrows(azureSdkDir, cancellationToken);
            await RunDotNetBuildGenerateCodeThrows(cancellationToken);
            
            Logger.LogInformation("GitHub-based TypeSpec compilation completed successfully");
        }

        protected virtual async Task<string> ExtractAzureSdkDirAsync()
        {
            Logger.LogInformation("Extracting Azure SDK directory using git rev-parse --show-toplevel");

            try
            {
                var result = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.GitExecutable,
                    "rev-parse --show-toplevel",
                    SdkOutputDir,
                    CancellationToken.None).ConfigureAwait(false);

                if (!result.Success)
                {
                    Logger.LogWarning("Git command failed, falling back to directory traversal. Error: {Error}", result.Error);
                    return ExtractAzureSdkDirFallback();
                }

                string gitRoot = result.Output.Trim();
                
                if (string.IsNullOrEmpty(gitRoot) || !Directory.Exists(gitRoot))
                {
                    Logger.LogWarning("Git returned invalid root path, falling back to directory traversal");
                    return ExtractAzureSdkDirFallback();
                }

                string gitDir = Path.Combine(gitRoot, ".git");
                if (!Directory.Exists(gitDir))
                {
                    Logger.LogWarning("Git root path does not contain .git directory, falling back to directory traversal");
                    return ExtractAzureSdkDirFallback();
                }

                Logger.LogInformation("Found Azure SDK directory via git: {AzureSdkDir}", gitRoot);
                return gitRoot;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to execute git command, falling back to directory traversal");
                return ExtractAzureSdkDirFallback();
            }
        }

        private string ExtractAzureSdkDirFallback()
        {
            Logger.LogInformation("Using directory traversal to find Azure SDK directory");
            
            string? currentDir = SdkOutputDir;
            string? azureSdkDirByName = null;
            const int maxIterations = 64;
            int iterations = 0;
            
            while (!string.IsNullOrEmpty(currentDir) && iterations < maxIterations)
            {
                iterations++;
                
                if (Directory.Exists(Path.Combine(currentDir, ".git")))
                {
                    Logger.LogInformation("Found git repository root via directory traversal: {GitRoot}", currentDir);
                    return currentDir;
                }
                
                if (azureSdkDirByName == null && 
                    Path.GetFileName(currentDir).Equals(AppSettings.AzureSdkDirectoryName, StringComparison.OrdinalIgnoreCase))
                {
                    azureSdkDirByName = currentDir;
                }
                
                string? parentPath = Path.GetDirectoryName(currentDir);
                if (parentPath == currentDir)
                {
                    break;
                }
                currentDir = parentPath;
            }
            
            if (iterations >= maxIterations)
            {
                Logger.LogWarning("Directory traversal reached maximum depth of {MaxIterations} levels, stopping to prevent infinite recursion", maxIterations);
            }
            
            if (!string.IsNullOrEmpty(azureSdkDirByName))
            {
                Logger.LogInformation("Found Azure SDK directory by name: {AzureSdkDir}", azureSdkDirByName);
                return azureSdkDirByName;
            }

            throw new InvalidOperationException("Could not locate azure-sdk-for-net directory from SDK output path");
        }

        private async Task RunPowerShellGenerationScriptThrows(string azureSdkPath, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running PowerShell generation script");

            string scriptPath = Path.Combine(azureSdkPath, AppSettings.PowerShellScriptPath);

            ValidationResult scriptValidation = InputValidator.ValidatePowerShellScriptPath(AppSettings.PowerShellScriptPath, azureSdkPath);
            if (!scriptValidation.IsValid)
            {
                Logger.LogError("PowerShell script validation failed: {Error}", scriptValidation.ErrorMessage);
                throw new InvalidOperationException($"PowerShell script validation failed: {scriptValidation.ErrorMessage}");
            }

            List<string> arguments = new List<string>
            {
                "-File", $"\"{scriptPath}\"",
                "-sdkFolder", $"\"{SdkOutputDir}\"",
                "-typespecSpecDirectory", $"\"{TypespecSpecDir}\"",
                "-commit", $"\"{CommitId}\"",
                "-repo", $"\"{AppSettings.AzureSpecRepository}\""
            };

            string argumentString = string.Join(" ", arguments);

            ValidationResult argValidation = InputValidator.ValidateProcessArguments(argumentString);
            if (!argValidation.IsValid)
            {
                Logger.LogError("PowerShell process arguments validation failed: {Error}", argValidation.ErrorMessage);
                throw new InvalidOperationException($"PowerShell process arguments validation failed: {argValidation.ErrorMessage}");
            }

            try
            {
                (bool success, string output, string error) = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.PowerShellExecutable,
                    argValidation.Value,
                    azureSdkPath,
                    cancellationToken).ConfigureAwait(false);

                if (!success)
                {
                    Logger.LogError("PowerShell generation script failed. Error: {Error}", error);
                    if (!string.IsNullOrEmpty(output))
                    {
                        Logger.LogError("PowerShell script standard output: {Output}", output);
                    }
                    throw new InvalidOperationException($"PowerShell generation script failed: {error}");
                }

                Logger.LogInformation("PowerShell generation script completed successfully");
            }
            catch (OperationCanceledException)
            {
                // Let cancellation exceptions bubble up
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error during PowerShell generation script execution");
                throw new InvalidOperationException("PowerShell generation script failed", ex);
            }
        }

        protected virtual async Task<bool> RunDotNetBuildGenerateCode(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running dotnet build /t:generateCode");

            string srcDirectory = Path.Combine(SdkOutputDir, "src");

            if (!Directory.Exists(srcDirectory))
            {
                Logger.LogError("Source directory not found: {SrcDirectory}", srcDirectory);
                return false;
            }

            try
            {
                (bool success, string output, string error) = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    "build /t:generateCode /p:Debug=True",
                    srcDirectory,
                    cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(output))
                {
                    Logger.LogInformation("dotnet build output: {Output}", output);
                }

                if (!success)
                {
                    Logger.LogError("dotnet build /t:generateCode failed with exit code. Error output: {Error}", error);
                    if (!string.IsNullOrEmpty(output))
                    {
                        Logger.LogError("dotnet build standard output: {Output}", output);
                    }
                    return false;
                }

                Logger.LogInformation("dotnet build /t:generateCode completed successfully");
                return true;
            }
            catch (OperationCanceledException)
            {
                // Let cancellation exceptions bubble up for this protected method too
                throw;
            }
        }

        private async Task RunDotNetBuildGenerateCodeThrows(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running dotnet build /t:generateCode");

            string srcDirectory = Path.Combine(SdkOutputDir, "src");

            if (!Directory.Exists(srcDirectory))
            {
                Logger.LogError("Source directory not found: {SrcDirectory}", srcDirectory);
                throw new InvalidOperationException($"Source directory not found: {srcDirectory}");
            }

            try
            {
                (bool success, string output, string error) = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    "build /t:generateCode /p:Debug=True",
                    srcDirectory,
                    cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(output))
                {
                    Logger.LogInformation("dotnet build output: {Output}", output);
                }

                if (!success)
                {
                    Logger.LogError("dotnet build /t:generateCode failed with exit code. Error output: {Error}", error);
                    if (!string.IsNullOrEmpty(output))
                    {
                        Logger.LogError("dotnet build standard output: {Output}", output);
                    }
                    throw new InvalidOperationException($"dotnet build /t:generateCode failed: {error}");
                }

                Logger.LogInformation("dotnet build /t:generateCode completed successfully");
            }
            catch (OperationCanceledException)
            {
                // Let cancellation exceptions bubble up
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error during dotnet build execution");
                throw new InvalidOperationException("Dotnet build generate code failed", ex);
            }
        }
    }
}
