using System.Runtime.InteropServices;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Exceptions;
using Azure.Tools.GeneratorAgent.Security;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    internal class LocalLibraryGenerationService
    {
        private readonly AppSettings AppSettings;
        private readonly ILogger<LocalLibraryGenerationService> Logger;
        private readonly ProcessExecutionService ProcessExecutionService;
        private readonly ValidationContext ValidationContext;

        public LocalLibraryGenerationService(
            AppSettings appSettings,
            ILogger<LocalLibraryGenerationService> logger,
            ProcessExecutionService processExecutionService,
            ValidationContext validationContext)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(processExecutionService);
            ArgumentNullException.ThrowIfNull(validationContext);

            AppSettings = appSettings;
            Logger = logger;
            ProcessExecutionService = processExecutionService;
            ValidationContext = validationContext;
        }

        public async Task<Result<object>> CompileTypeSpecAsync(CancellationToken cancellationToken = default)
        {
            string currentTypeSpecDir = ValidationContext.CurrentTypeSpecDir;

            Result<object> installResult = await InstallTypeSpecDependencies(cancellationToken).ConfigureAwait(false);
            if (installResult.IsFailure)
            {
                return installResult;
            }

            Result<object> compileResult = await CompileTypeSpec(cancellationToken).ConfigureAwait(false);
            if (compileResult.IsFailure)
            {
                return compileResult;
            }

            return Result<object>.Success("TypeSpec compilation completed successfully");
        }
        
        private async Task<Result<object>> InstallTypeSpecDependencies(CancellationToken cancellationToken)
        {
            Logger.LogDebug("Installing TypeSpec dependencies globally");

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
                return Result<object>.Failure(argValidation.Exception!);
            }

            string workingDirectory = Path.GetTempPath();

            Result<object> result = await ProcessExecutionService.ExecuteAsync(
                SecureProcessConfiguration.NpmExecutable,
                argValidation.Value!,
                workingDirectory,
                cancellationToken).ConfigureAwait(false);

            return result;
        }

        private async Task<Result<object>> CompileTypeSpec(CancellationToken cancellationToken)
        {
            Logger.LogDebug("Compiling TypeSpec project");

            string tspOutputPath = Path.Combine(ValidationContext.ValidatedSdkDir);
            string currentTypeSpecDir = ValidationContext.CurrentTypeSpecDir;

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
                return Result<object>.Failure(argValidation.Exception!);
            }

            Result<object> result = await ProcessExecutionService.ExecuteAsync(
                SecureProcessConfiguration.NpxExecutable,
                argValidation.Value!,
                currentTypeSpecDir,
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
    }
}
