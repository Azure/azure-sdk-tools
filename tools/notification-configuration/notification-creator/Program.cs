using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.WebApi;
using Azure.Sdk.Tools.NotificationConfiguration.Services;
using Azure.Sdk.Tools.NotificationConfiguration.Helpers;
using Azure.Identity;
using Azure.Core;
using Microsoft.VisualStudio.Services.Client;

namespace Azure.Sdk.Tools.NotificationConfiguration;

/// <summary>
/// A tool for creating and configuring Azure DevOps groups for sending email notifications
/// on build failures to owners of relevant build definitions. The recipients are determined
/// based on the build definition .yml file paths as given by the CODEOWNERS of given build definition
/// source repository.
/// </summary>
public static class Program
{
    /// <summary>
    /// Create notification groups for failures in scheduled builds
    /// </summary>
    /// <param name="organization">Azure DevOps Organization</param>
    /// <param name="project">Name of the DevOps project</param>
    /// <param name="pathPrefix">Path prefix to include pipelines (e.g. "\net")</param>
    /// <param name="selectionStrategy">Pipeline selection strategy</param>
    /// <param name="dryRun">Prints changes but does not alter any objects</param>
    /// <returns></returns>
    public static async Task Main(
        string organization,
        string project,
        string pathPrefix,
        PipelineSelectionStrategy selectionStrategy = PipelineSelectionStrategy.Scheduled,
        bool dryRun = false)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(config => { config.IncludeScopes = true; });
        });

        var logger = loggerFactory.CreateLogger(nameof(Program));

        logger.LogInformation(
            "Executing Azure.Sdk.Tools.NotificationConfiguration.Program.Main with following arguments: "
            + "organization: '{organization}' "
            + "project: '{project}' "
            + "pathPrefix: '{pathPrefix}' "
            + "selectionStrategy: '{selectionStrategy}' "
            + "dryRun: '{dryRun}' "
            , organization
            , project
            , pathPrefix
            , selectionStrategy
            , dryRun);

        var azureCredential = new ChainedTokenCredential(
            new AzureCliCredential(),
            new AzurePowerShellCredential()
        );

        var notificationConfigurator = new NotificationConfigurator(
            AzureDevOpsService(organization, azureCredential, loggerFactory),
            GitHubService(loggerFactory),
            loggerFactory.CreateLogger<NotificationConfigurator>());

        await notificationConfigurator.ConfigureNotifications(
            project,
            pathPrefix,
            GitHubToAADConverter(azureCredential, loggerFactory),
            persistChanges: !dryRun,
            strategy: selectionStrategy);
    }

    private static AzureDevOpsService AzureDevOpsService(
        string organization,
        TokenCredential azureCredential,
        ILoggerFactory loggerFactory)
    {
        var devOpsCreds = new VssAzureIdentityCredential(azureCredential);

        var devOpsConnection = new VssConnection(
            new Uri($"https://dev.azure.com/{organization}/"),
            devOpsCreds);

        var devOpsService = new AzureDevOpsService(
            devOpsConnection,
            loggerFactory.CreateLogger<AzureDevOpsService>());

        return devOpsService;
    }

    private static GitHubService GitHubService(ILoggerFactory loggerFactory)
        => new GitHubService(loggerFactory.CreateLogger<GitHubService>());

    private static GitHubToAADConverter GitHubToAADConverter(
        TokenCredential azureCredential,
        ILoggerFactory loggerFactory)
    {
        var githubToAadResolver = new GitHubToAADConverter(
            azureCredential,
            loggerFactory.CreateLogger<GitHubToAADConverter>()
        );

        return githubToAadResolver;
    }
}
