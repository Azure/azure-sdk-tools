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
            ValidationContext validationContext,
            AppSettings appSettings,
            ILoggerFactory loggerFactory,
            ProcessExecutor processExecutor)
        {
            ArgumentNullException.ThrowIfNull(validationContext);
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            ArgumentNullException.ThrowIfNull(processExecutor);

            if (string.IsNullOrWhiteSpace(validationContext.ValidatedCommitId))
            {
                return CreateForLocalPath(validationContext, appSettings, loggerFactory, processExecutor);
            }
            
            return CreateForGitHubCommit(validationContext, appSettings, loggerFactory, processExecutor);
        }

        private static ISdkGenerationService CreateForLocalPath(
            ValidationContext validationContext,
            AppSettings appSettings,
            ILoggerFactory loggerFactory,
            ProcessExecutor processExecutor)
        {
            return new LocalTypeSpecSdkGenerationService(
                appSettings,
                loggerFactory.CreateLogger<LocalTypeSpecSdkGenerationService>(),
                processExecutor,
                validationContext);
        }

        private static ISdkGenerationService CreateForGitHubCommit(
            ValidationContext validationContext,
            AppSettings appSettings,
            ILoggerFactory loggerFactory,
            ProcessExecutor processExecutor)
        {
            return new GitHubTypeSpecSdkGenerationService(
                appSettings,
                loggerFactory.CreateLogger<GitHubTypeSpecSdkGenerationService>(),
                processExecutor,
                validationContext);
        }
    }
}
