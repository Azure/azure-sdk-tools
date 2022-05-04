using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Azure.Sdk.Tools.NotificationConfiguration.Services;
using Azure.Sdk.Tools.NotificationConfiguration.Helpers;
using Azure.Identity;

namespace Azure.Sdk.Tools.NotificationConfiguration
{
    class Program
    {
        /// <summary>
        /// Create notification groups for failures in scheduled builds
        /// </summary>
        /// <param name="organization">Azure DevOps Organization</param>
        /// <param name="project">Name of the DevOps project</param>
        /// <param name="pathPrefix">Path prefix to include pipelines (e.g. "\net")</param>
        /// <param name="tokenVariableName">Environment variable token name (e.g. "SYSTEM_ACCESSTOKEN")</param>
        /// <param name="aadAppIdVar">AAD App ID environment variable name (OpensourceAPI access)</param>
        /// <param name="aadAppSecretVar">AAD App Secret environment variable name (OpensourceAPI access)</param>
        /// <param name="aadTenantVar">AAD Tenant environment variable name (OpensourceAPI access)</param>
        /// <param name="selectionStrategy">Pipeline selection strategy</param>
        /// <param name="dryRun">Prints changes but does not alter any objects</param>
        /// <returns></returns>
        static async Task Main(
            string organization,
            string project,
            string pathPrefix,
            string tokenVariableName,
            string aadAppIdVar,
            string aadAppSecretVar,
            string aadTenantVar,
            PipelineSelectionStrategy selectionStrategy = PipelineSelectionStrategy.Scheduled,
            bool dryRun = false)
        {
            var devOpsToken = Environment.GetEnvironmentVariable(tokenVariableName);
            var devOpsCreds = new VssBasicCredential("nobody", devOpsToken);
            var devOpsConnection = new VssConnection(new Uri($"https://dev.azure.com/{organization}/"), devOpsCreds);

#pragma warning disable CS0618 // Type or member is obsolete
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole(config => { config.IncludeScopes = true; });
            });
#pragma warning restore CS0618 // Type or member is obsolete
            var devOpsServiceLogger = loggerFactory.CreateLogger<AzureDevOpsService>();
            var notificationConfiguratorLogger = loggerFactory.CreateLogger<NotificationConfigurator>();

            var devOpsService = new AzureDevOpsService(devOpsConnection, devOpsServiceLogger);
            var gitHubService = new GitHubService(loggerFactory.CreateLogger<GitHubService>());
            var credential = new ClientSecretCredential(
                Environment.GetEnvironmentVariable(aadTenantVar),
                Environment.GetEnvironmentVariable(aadAppIdVar),
                Environment.GetEnvironmentVariable(aadAppSecretVar));
            var githubToAadResolver = new GitHubToAADConverter(
                credential,
                loggerFactory.CreateLogger<GitHubToAADConverter>()
            );
            var configurator = new NotificationConfigurator(devOpsService,
                gitHubService, notificationConfiguratorLogger);
            await configurator.ConfigureNotifications(
                project,
                pathPrefix,
                githubToAadResolver,
                persistChanges: !dryRun,
                strategy: selectionStrategy);

        }
    }
}
