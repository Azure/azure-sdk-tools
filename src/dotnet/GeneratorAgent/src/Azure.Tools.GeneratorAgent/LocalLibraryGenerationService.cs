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

        public LocalLibraryGenerationService(
            AppSettings appSettings,
            ILogger<LocalLibraryGenerationService> logger,
            ProcessExecutionService processExecutionService)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(processExecutionService);

            AppSettings = appSettings;
            Logger = logger;
            ProcessExecutionService = processExecutionService;
        }
        
        public async Task InstallTypeSpecDependencies(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Installing TypeSpec dependencies globally");

            string arguments;
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                arguments = $"-Command \"npm install --global {AppSettings.TypespecCompiler} {AppSettings.TypespecEmitterPackage}\"";
            }
            else
            {
                arguments = $"install --global {AppSettings.TypespecCompiler} {AppSettings.TypespecEmitterPackage}";
            }

            string validatedArguments = InputValidator.ValidateProcessArguments(arguments);

            Result<object> result = await ProcessExecutionService.ExecuteAsync(
                SecureProcessConfiguration.NpmExecutable,
                validatedArguments,
                Path.GetTempPath(),
                cancellationToken,
                TimeSpan.FromMinutes(3)).ConfigureAwait(false);

            if (result.IsFailure)
            {
                string errorMessage = result.ProcessException!.Error;
                throw new InvalidOperationException($"Failed to install TypeSpec dependencies: {errorMessage}");
            }
        }

        public async Task<Result<object>> CompileTypeSpecAsync(ValidationContext validationContext, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(validationContext);
            
            Logger.LogDebug("Compiling TypeSpec project");

            string tspOutputPath = Path.Combine(validationContext.ValidatedSdkDir);
            string currentTypeSpecDir = validationContext.CurrentTypeSpecDir;

            string arguments;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                arguments = $"-Command \"npx tsp compile . --emit {AppSettings.TypespecEmitterPackage} --option '{AppSettings.TypespecEmitterPackage}.emitter-output-dir={tspOutputPath}'\"";
            }
            else
            {
                arguments = $"tsp compile . --emit {AppSettings.TypespecEmitterPackage} --option \"{AppSettings.TypespecEmitterPackage}.emitter-output-dir={tspOutputPath}\"";
            }
            
            string validatedArguments = InputValidator.ValidateProcessArguments(arguments);

            return await ProcessExecutionService.ExecuteAsync(
                SecureProcessConfiguration.NpxExecutable,
                validatedArguments,
                currentTypeSpecDir,
                cancellationToken,
                TimeSpan.FromMinutes(5)).ConfigureAwait(false);
        }
    }
}
