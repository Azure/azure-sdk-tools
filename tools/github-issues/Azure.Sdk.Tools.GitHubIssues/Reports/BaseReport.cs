using Azure.Sdk.Tools.GitHubIssues.Services.Configuration;
using Azure.Security.KeyVault.Secrets;
using GitHubIssues.Helpers;
using Microsoft.Extensions.Logging;
using Octokit;
using Polly;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubIssues.Reports
{
    public abstract class BaseReport
    {
        protected IConfigurationService ConfigurationService { get; private set; }

        public BaseReport(IConfigurationService configurationService)
        {
            ConfigurationService = configurationService;
        }

        public async Task ExecuteAsync(ILogger log)
        {
            Task<IEnumerable<RepositoryConfiguration>> pendingRepositoryConfigurations = ConfigurationService.GetRepositoryConfigurationsAsync();
            Task<string> pendingGitHubPersonalAccessToken = ConfigurationService.GetGitHubPersonalAccessTokenAsync();
            Task<string> pendingSendGridToken = ConfigurationService.GetSendGridTokenAsync();
            Task<string> pendingFromAddress = ConfigurationService.GetFromAddressAsync();

            var gitHubClient = new GitHubClient(new ProductHeaderValue("github-issues"))
            {
                Credentials = new Credentials(await pendingGitHubPersonalAccessToken)
            };

            var context = new ReportExecutionContext(
                log,
                await pendingFromAddress,
                await pendingSendGridToken,
                await pendingGitHubPersonalAccessToken,
                await pendingRepositoryConfigurations,
                gitHubClient
                );

            await ExecuteCoreAsync(context);
        }

        protected abstract Task ExecuteCoreAsync(ReportExecutionContext context);
    }
}
