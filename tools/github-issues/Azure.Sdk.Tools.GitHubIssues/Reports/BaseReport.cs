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
            await Policy
                .Handle<AbuseException>()
                .Or<RateLimitExceededException>()
                .RetryAsync(3, async (ex, retryCount) =>
                {
                TimeSpan retryDelay = TimeSpan.FromSeconds(10); // Default.

                    switch (ex)
                    {
                        case AbuseException abuseException:
                            retryDelay = TimeSpan.FromSeconds((double)abuseException.RetryAfterSeconds);
                            log.LogWarning("Abuse exception detected. Retry after seconds is: {retrySeconds}",
                                abuseException.RetryAfterSeconds
                                );
                            break;

                        case RateLimitExceededException rateLimitExceededException:
                            retryDelay = rateLimitExceededException.GetRetryAfterTimeSpan();
                            log.LogWarning(
                                "Rate limit exception detected. Limit is: {limit}, reset is: {reset}, retry seconds is: {retrySeconds}",
                                rateLimitExceededException.Limit,
                                rateLimitExceededException.Reset,
                                retryDelay.TotalSeconds
                                );
                            break;
                    }
                })
                .ExecuteAsync(async () =>
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
                });
        }

        protected abstract Task ExecuteCoreAsync(ReportExecutionContext context);
    }
}
