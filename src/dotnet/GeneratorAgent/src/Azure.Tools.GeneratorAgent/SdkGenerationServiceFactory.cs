using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Factory for creating SDK generation service instances.
    /// </summary>
    internal static class SdkGenerationServiceFactory
    {
        public static ISdkGenerationService CreateSdkGenerationService(
            string? typeSpecDir,
            string? commitId,
            string sdkDir,
            AppSettings appSettings,
            ILoggerFactory loggerFactory,
            ProcessExecutor processExecutor)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sdkDir);
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            ArgumentNullException.ThrowIfNull(processExecutor);

            if (string.IsNullOrWhiteSpace(commitId))
            {
                return CreateForLocalPath(typeSpecDir!, sdkDir, appSettings, loggerFactory, processExecutor);
            }
            
            return CreateForGitHubCommit(commitId, typeSpecDir!, sdkDir, appSettings, loggerFactory, processExecutor);
        }

        private static ISdkGenerationService CreateForLocalPath(
            string typeSpecDir,
            string sdkDir,
            AppSettings appSettings,
            ILoggerFactory loggerFactory,
            ProcessExecutor processExecutor)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(typeSpecDir);

            ValidationContext validationContext = ValidationContext.CreateFromValidatedInputs(
                typeSpecDir, 
                string.Empty,
                sdkDir);

            return new LocalTypeSpecSdkGenerationService(
                appSettings,
                loggerFactory.CreateLogger<LocalTypeSpecSdkGenerationService>(),
                processExecutor,
                validationContext);
        }

        private static ISdkGenerationService CreateForGitHubCommit(
            string commitId,
            string typespecSpecDirectory,
            string sdkOutputDirectory,
            AppSettings appSettings,
            ILoggerFactory loggerFactory,
            ProcessExecutor processExecutor)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(typespecSpecDirectory);

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
