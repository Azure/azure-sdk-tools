using Microsoft.Extensions.Logging;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Security;
using System.Runtime.InteropServices;

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
            Logger.LogInformation("\n Starting TypeSpec compilation for project: {ProjectPath} \n", TypespecDir);

            // Step 1: Install dependencies
            Result installResult = await InstallTypeSpecDependencies(cancellationToken);
            if (installResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to install TypeSpec dependencies: {installResult.Error}");
            }

            // Step 2: Compile TypeSpec
            Result compileResult = await CompileTypeSpec(cancellationToken);
            if (compileResult.IsFailure)
            {
                // TODO: Send compileResult.Error to AI for analysis
                throw new InvalidOperationException($"Failed to compile TypeSpec: {compileResult.Error}");
            }

            Logger.LogInformation("\n TypeSpec compilation completed successfully \n");
        }
        
        private async Task<Result> InstallTypeSpecDependencies(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Installing TypeSpec dependencies globally....\n");

            string arguments;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                arguments = $"-Command \"npm install --global {AppSettings.TypespecEmitterPackage}\"";
            }
            else
            {
                arguments = $"install --global {AppSettings.TypespecEmitterPackage}";
            }

            Result<string> argValidation = InputValidator.ValidateProcessArguments(arguments);
            if (argValidation.IsFailure)
            {
                return Result.Failure($"Process arguments validation failed: {argValidation.Error}");
            }

            string workingDirectory = Path.GetTempPath();

            try
            {
                Result result = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.NpmExecutable,
                    argValidation.Value,
                    workingDirectory,
                    cancellationToken).ConfigureAwait(false);

                if (result.IsFailure)
                {
                    return Result.Failure(result.Error);
                }

                Logger.LogInformation("TypeSpec dependencies installed globally successfully..\n");
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

        private async Task<Result> CompileTypeSpec(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Compiling TypeSpec project...\n");

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
            
            Result<string> argValidation = InputValidator.ValidateProcessArguments(arguments);
            if (argValidation.IsFailure)
            {
                return Result.Failure($"Process arguments validation failed: {argValidation.Error}");
            }

            try
            {
                Result result = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.NpxExecutable,
                    argValidation.Value,
                    TypespecDir,
                    cancellationToken).ConfigureAwait(false);

                if (result.IsFailure)
                {
                    return Result.Failure(result.Error);
                }

                Logger.LogInformation("TypeSpec compilation completed \n");
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
