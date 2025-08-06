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
            Logger.LogInformation("\n Starting GitHub-based TypeSpec compilation for commit: {CommitId}", CommitId);

            string azureSdkDir = await ExtractAzureSdkDirAsync();

            // Step 1: Run PowerShell generation script
            Result powerShellResult = await RunPowerShellGenerationScriptAsync(azureSdkDir, cancellationToken);
            if (powerShellResult.IsFailure)
            {
                throw new InvalidOperationException($"PowerShell generation script failed: {powerShellResult.Error}");
            }

            // Step 2: Run dotnet build generateCode
            Result buildResult = await RunDotNetBuildGenerateCodeAsync(cancellationToken);
            if (buildResult.IsFailure)
            {
                // TODO: Send buildResult.Error to AI for analysis
                throw new InvalidOperationException($"dotnet build /t:generateCode failed: {buildResult.Error}");
            }
            
            Logger.LogInformation("GitHub-based TypeSpec compilation completed successfully");
        }

        protected virtual async Task<string> ExtractAzureSdkDirAsync()
        {
            try
            {
                Result result = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.GitExecutable,
                    "rev-parse --show-toplevel",
                    SdkOutputDir,
                    CancellationToken.None).ConfigureAwait(false);

                if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
                {
                    string gitRoot = result.Output.Trim();
                    
                    if (Directory.Exists(gitRoot) && Directory.Exists(Path.Combine(gitRoot, ".git")))
                    {
                        Logger.LogInformation("Found Azure SDK directory via git: {AzureSdkDir}", gitRoot);
                        return gitRoot;
                    }
                    
                    Logger.LogWarning("Git root path does not contain .git directory, falling back to directory traversal");
                }
                else
                {
                    Logger.LogWarning("Git command failed, falling back to directory traversal. Error: {Error}", result.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to execute git command, falling back to directory traversal");
            }

            return ExtractAzureSdkDirFallback();
        }

        private string ExtractAzureSdkDirFallback()
        {
            string? currentDir = SdkOutputDir;
            string? azureSdkDirByName = null;
            const int maxIterations = 64;
            int iterations = 0;
            
            while (!string.IsNullOrEmpty(currentDir) && iterations < maxIterations)
            {
                iterations++;
                
                if (Directory.Exists(Path.Combine(currentDir, ".git")))
                {
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

        private async Task<Result> RunPowerShellGenerationScriptAsync(string azureSdkPath, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running PowerShell generation script \n");

            string scriptPath = Path.Combine(azureSdkPath, AppSettings.PowerShellScriptPath);

            Result<string> scriptValidation = InputValidator.ValidatePowerShellScriptPath(AppSettings.PowerShellScriptPath, azureSdkPath);
            if (scriptValidation.IsFailure)
            {
                return Result.Failure($"PowerShell script validation failed: {scriptValidation.Error}");
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

            Result<string> argValidation = InputValidator.ValidateProcessArguments(argumentString);
            if (argValidation.IsFailure)
            {
                return Result.Failure($"PowerShell process arguments validation failed: {argValidation.Error}");
            }

            try
            {
                Result result = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.PowerShellExecutable,
                    argValidation.Value,
                    azureSdkPath,
                    cancellationToken).ConfigureAwait(false);

                if (result.IsFailure)
                {
                    return Result.Failure(result.Error);
                }

                Logger.LogInformation("PowerShell generation script completed successfully");
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Result.Failure(ex.Message, ex);
            }
        }

        private async Task<Result> RunDotNetBuildGenerateCodeAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running dotnet build /t:generateCode");

            string srcDirectory = Path.Combine(SdkOutputDir, "src");

            if (!Directory.Exists(srcDirectory))
            {
                return Result.Failure($"Source directory not found: {srcDirectory}");
            }

            try
            {
                Result result = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    "build /t:generateCode /p:Debug=True",
                    srcDirectory,
                    cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(result.Output))
                {
                    Logger.LogInformation("dotnet build output: {Output}", result.Output);
                }

                if (result.IsFailure)
                {
                    return Result.Failure(result.Error);
                }

                Logger.LogInformation("dotnet build /t:generateCode completed successfully");
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Result.Failure(ex.Message, ex);
            }
        }
    }
}
