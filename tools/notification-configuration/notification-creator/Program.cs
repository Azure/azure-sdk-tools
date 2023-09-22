using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Azure.Sdk.Tools.NotificationConfiguration.Services;
using Azure.Sdk.Tools.NotificationConfiguration.Helpers;
using Azure.Identity;

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
    /// <param name="tokenVariableName">Environment variable token name (e.g. "SYSTEM_ACCESSTOKEN")</param>
    /// <param name="aadAppIdVar">AAD App ID environment variable name (OpensourceAPI access)</param>
    /// <param name="aadAppSecretVar">AAD App Secret environment variable name (OpensourceAPI access)</param>
    /// <param name="aadTenantVar">AAD Tenant environment variable name (OpensourceAPI access)</param>
    /// <param name="selectionStrategy">Pipeline selection strategy</param>
    /// <param name="dryRun">Prints changes but does not alter any objects</param>
    /// <returns></returns>
    public static async Task Main(
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
            + "tokenVariableName: '{tokenVariableName}' "
            + "aadAppIdVar: '{aadAppIdVar}' "
            + "aadAppSecretVar: '{aadAppSecretVar}' "
            + "aadTenantVar: '{aadTenantVar}' "
            + "selectionStrategy: '{selectionStrategy}' "
            + "dryRun: '{dryRun}' "
            , organization
            , project
            , pathPrefix
            , tokenVariableName
            , aadAppIdVar
            , aadAppSecretVar
            , aadTenantVar
            , selectionStrategy
            , dryRun);

        var notificationConfigurator = new NotificationConfigurator(
            AzureDevOpsService(organization, tokenVariableName, loggerFactory),
            GitHubService(loggerFactory),
            loggerFactory.CreateLogger<NotificationConfigurator>());

        await notificationConfigurator.ConfigureNotifications(
            project,
            pathPrefix,
            GitHubToAADConverter(aadTenantVar, aadAppIdVar, aadAppSecretVar, loggerFactory),
            persistChanges: !dryRun,
            strategy: selectionStrategy);
    }

    private static AzureDevOpsService AzureDevOpsService(
        string organization,
        string tokenVariableName,
        ILoggerFactory loggerFactory)
    {
        var devOpsToken = Environment.GetEnvironmentVariable(tokenVariableName);
        var devOpsCreds = new VssBasicCredential("nobody", devOpsToken);
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
        string aadTenantVar,
        string aadAppIdVar,
        string aadAppSecretVar,
        ILoggerFactory loggerFactory)
    {
        var credential = new ClientSecretCredential(
            Environment.GetEnvironmentVariable(aadTenantVar),
            Environment.GetEnvironmentVariable(aadAppIdVar),
            Environment.GetEnvironmentVariable(aadAppSecretVar));

        var githubToAadResolver = new GitHubToAADConverter(
            credential,
            loggerFactory.CreateLogger<GitHubToAADConverter>()
        );
        return githubToAadResolver;
    }
}
