using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Factory for creating SDK generation service instances.
    /// </summary>
    internal static class SdkGenerationServiceFactory
    {
        private static void ValidateCommonParameters(AppSettings appSettings, ILoggerFactory loggerFactory, ProcessExecutor processExecutor)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            ArgumentNullException.ThrowIfNull(processExecutor);
        }

        public static ISdkGenerationService CreateForLocalPath(
            string typeSpecDir,
            string sdkDir,
            AppSettings appSettings,
            ILoggerFactory loggerFactory,
            ProcessExecutor processExecutor)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(typeSpecDir);
            ArgumentException.ThrowIfNullOrWhiteSpace(sdkDir);
            ValidateCommonParameters(appSettings, loggerFactory, processExecutor);

            ValidationContext validationContext = ValidationContext.CreateFromValidatedInputs(
                typeSpecDir, 
                string.Empty, // commitId not used for local path
                sdkDir);

            return new LocalTypeSpecSdkGenerationService(
                appSettings,
                loggerFactory.CreateLogger<LocalTypeSpecSdkGenerationService>(),
                processExecutor,
                validationContext);
        }

        public static ISdkGenerationService CreateForGitHubCommit(
            string commitId,
            string typespecSpecDirectory,
            string sdkOutputDirectory,
            AppSettings appSettings,
            ILoggerFactory loggerFactory,
            ProcessExecutor processExecutor)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(commitId);
            ArgumentException.ThrowIfNullOrWhiteSpace(typespecSpecDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(sdkOutputDirectory);
            ValidateCommonParameters(appSettings, loggerFactory, processExecutor);

            // Note: Validation is already done in CommandLineConfiguration.ValidateInput()
            // No need to re-validate here - inputs are trusted at this point
            ValidationContext validationContext = ValidationContext.CreateFromValidatedInputs(
                typespecSpecDirectory, 
                commitId, 
                sdkOutputDirectory);

            return new GitHubTypeSpecSdkGenerationService(
                appSettings,
                loggerFactory.CreateLogger<GitHubTypeSpecSdkGenerationService>(),
                processExecutor,
                validationContext);
        }
    }
}
