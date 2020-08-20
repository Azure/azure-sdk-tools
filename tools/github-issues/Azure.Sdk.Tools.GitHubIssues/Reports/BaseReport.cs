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
            try
            {
                Task<string> pendingGitHubPersonalAccessToken = ConfigurationService.GetGitHubPersonalAccessTokenAsync();

                var gitHubClient = new GitHubClient(new ProductHeaderValue("github-issues"))
                {
                    Credentials = new Credentials(await pendingGitHubPersonalAccessToken)
                };

                log.LogInformation("Preparing report execution context for: {reportType}", this.GetType());

                var context = new ReportExecutionContext(
                    log,
                    await ConfigurationService.GetFromAddressAsync(),
                    await ConfigurationService.GetSendGridTokenAsync(),
                    await pendingGitHubPersonalAccessToken,
                    await ConfigurationService.GetRepositoryConfigurationsAsync(),
                    gitHubClient
                    );

                log.LogInformation("Executing report: {reportType}", this.GetType());
                await ExecuteCoreAsync(context);
                log.LogInformation("Executed report: {reportType}", this.GetType());
            }
            catch (RateLimitExceededException ex)
            {
                log.LogError("GitHub rate limit exceeded for report {reportType}. Rate limit is {callsPerHour} calls per Hour. Limit resets at {limitResets}.",
                    this.GetType(),
                    ex.Limit,
                    ex.Reset
                    );

                throw ex;
            }
        }

        protected abstract Task ExecuteCoreAsync(ReportExecutionContext context);
    }
}
