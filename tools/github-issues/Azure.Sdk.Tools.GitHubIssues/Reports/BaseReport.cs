using Azure.Sdk.Tools.GitHubIssues.Services.Configuration;
using Azure.Security.KeyVault.Secrets;
using GitHubIssues.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
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
        protected ILogger<BaseReport> Logger { get; private set; }

        public BaseReport(IConfigurationService configurationService, ILogger<BaseReport> logger)
        {
            ConfigurationService = configurationService;
            Logger = logger;
        }

        protected async Task ExecuteWithRetryAsync(int retryCount, Func<Task> action)
        {
            await Policy
                .Handle<AbuseException>()
                .Or<RateLimitExceededException>()
                .RetryAsync(10, async (ex, retryCount) =>
                {
                    TimeSpan retryDelay = TimeSpan.FromSeconds(10); // Default.

                    switch (ex)
                    {
                        case AbuseException abuseException:
                            retryDelay = TimeSpan.FromSeconds((double)abuseException.RetryAfterSeconds);
                            Logger.LogWarning("Abuse exception detected. Retry after seconds is: {retrySeconds}",
                                abuseException.RetryAfterSeconds
                                );
                            break;

                        case RateLimitExceededException rateLimitExceededException:
                            retryDelay = rateLimitExceededException.GetRetryAfterTimeSpan();
                            Logger.LogWarning(
                                "Rate limit exception detected. Limit is: {limit}, reset is: {reset}, retry seconds is: {retrySeconds}",
                                rateLimitExceededException.Limit,
                                rateLimitExceededException.Reset,
                                retryDelay.TotalSeconds
                                );
                            break;

                        default:
                            Logger.LogError(
                                "Fall through case invoked, this should never happen!"
                                );
                            break;
                    }

                    await Task.Delay(retryDelay);
                })
                .ExecuteAsync(async () =>
                {
                    try
                    {
                        await action();
                    }
                    catch (AggregateException ex) when (ex.InnerException! is AbuseException || ex.InnerException! is RateLimitExceededException)
                    {
                        throw ex.InnerException;
                    }
                });
        }

        public async Task ExecuteAsync()
        {
            Task<string> pendingGitHubPersonalAccessToken = ConfigurationService.GetGitHubPersonalAccessTokenAsync();

            var gitHubClient = new GitHubClient(new ProductHeaderValue("github-issues"))
            {
                Credentials = new Credentials(await pendingGitHubPersonalAccessToken)
            };

            Logger.LogInformation("Preparing report execution context for: {reportType}", this.GetType());

            var context = new ReportExecutionContext(
                await ConfigurationService.GetFromAddressAsync(),
                await ConfigurationService.GetSendGridTokenAsync(),
                await pendingGitHubPersonalAccessToken,
                await ConfigurationService.GetRepositoryConfigurationsAsync(),
                gitHubClient
                );

            Logger.LogInformation("Executing report: {reportType}", this.GetType());
            await ExecuteCoreAsync(context);
            Logger.LogInformation("Executed report: {reportType}", this.GetType());
        }

        protected abstract Task ExecuteCoreAsync(ReportExecutionContext context);
    }
}
