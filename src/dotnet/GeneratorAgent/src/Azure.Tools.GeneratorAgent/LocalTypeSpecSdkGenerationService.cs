using Microsoft.Extensions.Logging;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Security;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Tools.GeneratorAgent
{
    internal class LocalTypeSpecSdkGenerationService : ISdkGenerationService
    {
        private readonly AppSettings AppSettings;
        private readonly ILogger<LocalTypeSpecSdkGenerationService> Logger;
        private readonly ProcessExecutor ProcessExecutor;
        private readonly string TypespecDir;
        private readonly string SdkDir;

        public LocalTypeSpecSdkGenerationService(
            AppSettings appSettings,
            ILogger<LocalTypeSpecSdkGenerationService> logger,
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

            TypespecDir = validationContext.ValidatedTypeSpecDir;
            SdkDir = validationContext.ValidatedSdkDir;
        }

        public async Task CompileTypeSpecAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Starting TypeSpec compilation for project: {ProjectPath}", TypespecDir);

            await InstallTypeSpecDependenciesThrows(cancellationToken);
            await CompileTypeSpecThrows(cancellationToken);

            Logger.LogInformation("TypeSpec compilation completed successfully");
        }

        protected virtual async Task<bool> InstallTypeSpecDependencies(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Installing TypeSpec dependencies globally");

            string arguments;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows, use PowerShell to execute npm reliably
                arguments = $"-Command \"npm install --global {AppSettings.TypespecEmitterPackage}\"";
            }
            else
            {
                // On Unix/Linux/macOS, use npm directly
                arguments = $"install --global {AppSettings.TypespecEmitterPackage}";
            }
            
            ValidationResult argValidation = InputValidator.ValidateProcessArguments(arguments);
            if (!argValidation.IsValid)
            {
                Logger.LogError("Process arguments validation failed: {Error}", argValidation.ErrorMessage);
                return false;
            }

            string workingDirectory = Path.GetTempPath();

            try
            {
                (bool success, string output, string error) = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.NpmExecutable,
                    argValidation.Value,
                    workingDirectory,
                    cancellationToken).ConfigureAwait(false);

                if (!success)
                {
                    Logger.LogError("Global npm install failed. Error: {Error}", error);
                    return false;
                }

                Logger.LogInformation("TypeSpec dependencies installed globally successfully: {Output}", output);
                return true;
            }
            catch (OperationCanceledException)
            {
                // Let cancellation exceptions bubble up for this protected method too
                throw;
            }
        }

        private async Task InstallTypeSpecDependenciesThrows(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Installing TypeSpec dependencies globally");

            string arguments;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows, use PowerShell to execute npm reliably
                arguments = $"-Command \"npm install --global {AppSettings.TypespecEmitterPackage}\"";
            }
            else
            {
                // On Unix/Linux/macOS, use npm directly
                arguments = $"install --global {AppSettings.TypespecEmitterPackage}";
            }
            
            ValidationResult argValidation = InputValidator.ValidateProcessArguments(arguments);
            if (!argValidation.IsValid)
            {
                Logger.LogError("Process arguments validation failed: {Error}", argValidation.ErrorMessage);
                throw new InvalidOperationException($"Process arguments validation failed: {argValidation.ErrorMessage}");
            }

            string workingDirectory = Path.GetTempPath();

            try
            {
                (bool success, string output, string error) = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.NpmExecutable,
                    argValidation.Value,
                    workingDirectory,
                    cancellationToken).ConfigureAwait(false);

                if (!success)
                {
                    Logger.LogError("Global npm install failed. Error: {Error}", error);
                    throw new InvalidOperationException($"Global npm install failed: {error}");
                }

                Logger.LogInformation("TypeSpec dependencies installed globally successfully: {Output}", output);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error during TypeSpec dependencies installation");
                throw new InvalidOperationException("Failed to install TypeSpec dependencies", ex);
            }
        }

        protected virtual async Task<bool> CompileTypeSpec(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Compiling TypeSpec project");

            string tspOutputPath = Path.Combine(SdkDir);

            string arguments;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                arguments = $"-Command \"npx tsp compile . --emit {AppSettings.TypespecEmitterPackage} --option '{AppSettings.TypespecEmitterPackage}.emitter-output-dir={tspOutputPath}'\"";
            }
            else
            {
                arguments = $"tsp compile . --emit {AppSettings.TypespecEmitterPackage} --option \"{AppSettings.TypespecEmitterPackage}.emitter-output-dir={tspOutputPath}\"";
            }
            
            ValidationResult argValidation = InputValidator.ValidateProcessArguments(arguments);
            if (!argValidation.IsValid)
            {
                Logger.LogError("Process arguments validation failed: {Error}", argValidation.ErrorMessage);
                return false;
            }

            try
            {
                (bool success, string output, string error) = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.NpxExecutable,
                    argValidation.Value,
                    TypespecDir,
                    cancellationToken).ConfigureAwait(false);

                if (!success)
                {
                    Logger.LogError("TypeSpec compilation failed. Error: {Error}", error);
                    return false;
                }

                Logger.LogInformation("TypeSpec compilation completed: {Output}", output);
                return true;
            }
            catch (OperationCanceledException)
            {
                // Let cancellation exceptions bubble up for this protected method too
                throw;
            }
        }

        private async Task CompileTypeSpecThrows(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Compiling TypeSpec project");

            string tspOutputPath = Path.Combine(SdkDir);

            string arguments;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                arguments = $"-Command \"npx tsp compile . --emit {AppSettings.TypespecEmitterPackage} --option '{AppSettings.TypespecEmitterPackage}.emitter-output-dir={tspOutputPath}'\"";
            }
            else
            {
                arguments = $"tsp compile . --emit {AppSettings.TypespecEmitterPackage} --option \"{AppSettings.TypespecEmitterPackage}.emitter-output-dir={tspOutputPath}\"";
            }
            
            ValidationResult argValidation = InputValidator.ValidateProcessArguments(arguments);
            if (!argValidation.IsValid)
            {
                Logger.LogError("Process arguments validation failed: {Error}", argValidation.ErrorMessage);
                throw new InvalidOperationException($"Process arguments validation failed: {argValidation.ErrorMessage}");
            }

            try
            {
                (bool success, string output, string error) = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.NpxExecutable,
                    argValidation.Value,
                    TypespecDir,
                    cancellationToken).ConfigureAwait(false);

                if (!success)
                {
                    Logger.LogError("TypeSpec compilation failed. Error: {Error}", error);
                    throw new InvalidOperationException($"TypeSpec compilation failed: {error}");
                }

                Logger.LogInformation("TypeSpec compilation completed: {Output}", output);
            }
            catch (OperationCanceledException)
            {
                // Let cancellation exceptions bubble up
                throw;
            }
            catch (TimeoutException)
            {
                // Let timeout exceptions bubble up
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error during TypeSpec compilation");
                throw new InvalidOperationException("Failed to compile TypeSpec project", ex);
            }
        }
    }
}
