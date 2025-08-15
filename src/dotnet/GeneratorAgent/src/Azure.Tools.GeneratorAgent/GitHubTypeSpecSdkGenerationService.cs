using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Exceptions;
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

        public async Task<Result<object>> CompileTypeSpecAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Starting TypeSpec compilation for commit: {CommitId}", CommitId);

            Result<string> sdkDirResult = await ExtractAzureSdkDirAsync();
            if (sdkDirResult.IsFailure)
            {
                return Result<object>.Failure(sdkDirResult.Exception ?? new InvalidOperationException("Failed to extract Azure SDK directory"));
            }

            Result<object> powerShellResult = await RunPowerShellGenerationScriptAsync(sdkDirResult.Value!, cancellationToken);
            if (powerShellResult.IsFailure)
            {
                return powerShellResult;
            }

            Result<object> buildResult = await RunDotNetBuildGenerateCodeAsync(cancellationToken);
            if (buildResult.IsFailure)
            {
                return buildResult;
            }

            Logger.LogInformation("TypeSpec compilation completed successfully");
            return Result<object>.Success("TypeSpec compilation completed successfully");
        }

        protected virtual async Task<Result<string>> ExtractAzureSdkDirAsync()
        {
            Exception? gitException = null;
            
            try
            {
                Result<object> result = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.GitExecutable,
                    "rev-parse --show-toplevel",
                    SdkOutputDir,
                    CancellationToken.None).ConfigureAwait(false);

                if (result.IsSuccess && result.Value is string output && !string.IsNullOrEmpty(output))
                {
                    string gitRoot = output.Trim();
                    if (Directory.Exists(gitRoot))
                    {
                        return Result<string>.Success(gitRoot);
                    }
                }
                
                gitException = result.ProcessException ?? result.Exception;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                gitException = ex;
                Logger.LogWarning(ex, "Failed to execute git command, falling back to directory traversal");
            }

            Result<string> fallbackResult = ExtractAzureSdkDirFallback();
            
            if (fallbackResult.IsFailure && gitException != null)
            {
                return Result<string>.Failure(new InvalidOperationException(
                    $"Both git detection and directory traversal failed. Git error: {gitException.Message}. Traversal error: {fallbackResult.Exception?.Message}",
                    gitException));
            }
            
            return fallbackResult;
        }

        private Result<string> ExtractAzureSdkDirFallback()
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
                    return Result<string>.Success(currentDir);
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
            
            if (!string.IsNullOrEmpty(azureSdkDirByName))
            {
                Logger.LogInformation("Found Azure SDK directory by name: {AzureSdkDir}", azureSdkDirByName);
                return Result<string>.Success(azureSdkDirByName);
            }

            if (iterations >= maxIterations)
            {
                Logger.LogWarning("Directory traversal reached maximum depth of {MaxIterations} levels, stopping to prevent infinite recursion", maxIterations);
            }

            return Result<string>.Failure(new InvalidOperationException("Could not locate azure-sdk-for-net directory from SDK output path"));
        }

        private async Task<Result<object>> RunPowerShellGenerationScriptAsync(string azureSdkPath, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running PowerShell generation script");

            string scriptPath = Path.Combine(azureSdkPath, AppSettings.PowerShellScriptPath);

            Result<string> scriptValidation = InputValidator.ValidatePowerShellScriptPath(AppSettings.PowerShellScriptPath, azureSdkPath);
            if (scriptValidation.IsFailure)
            {
                throw new ArgumentException($"PowerShell script validation failed: {scriptValidation.Exception?.Message}");
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
                throw new ArgumentException($"PowerShell process arguments validation failed: {argValidation.Exception?.Message}");
            }

            try
            {
                Result<object> result = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.PowerShellExecutable,
                    argValidation.Value!,
                    azureSdkPath,
                    cancellationToken).ConfigureAwait(false);

                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogCritical(ex, "Unexpected system error during PowerShell generation script");
                throw;
            }
        }

        private async Task<Result<object>> RunDotNetBuildGenerateCodeAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running dotnet build /t:generateCode");

            string srcDirectory = Path.Combine(SdkOutputDir, "src");

            if (!Directory.Exists(srcDirectory))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {srcDirectory}");
            }

            try
            {
                Result<object> result = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    "build /t:generateCode /p:Debug=True",
                    srcDirectory,
                    cancellationToken).ConfigureAwait(false);

                if (result.IsFailure && result.ProcessException != null)
                {
                    return Result<object>.Failure(
                        new TypeSpecCompilationException(
                            result.ProcessException.Command,
                            result.ProcessException.Output,
                            result.ProcessException.Error,
                            result.ProcessException.ExitCode ?? -1,
                            result.ProcessException));
                }
                
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogCritical(ex, "Unexpected system error during dotnet build");
                throw;
            }
        }
    }
}

