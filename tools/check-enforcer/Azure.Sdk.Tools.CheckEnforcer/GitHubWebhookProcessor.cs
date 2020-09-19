using Azure.Core;
using Azure.Identity;
using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Handlers;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Octokit;
using Octokit.Internal;
using Polly;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class GitHubWebhookProcessor
    {
        public GitHubWebhookProcessor(IGlobalConfigurationProvider globalConfigurationProvider, IGitHubClientProvider gitHubClientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider, SecretClient secretClient, GitHubRateLimiter limiter)
        {
            this.globalConfigurationProvider = globalConfigurationProvider;
            this.gitHubClientProvider = gitHubClientProvider;
            this.repositoryConfigurationProvider = repositoryConfigurationProvider;
            this.secretClient = secretClient;
            this.limiter = limiter;
        }

        public IGlobalConfigurationProvider globalConfigurationProvider;
        public IGitHubClientProvider gitHubClientProvider;
        private IRepositoryConfigurationProvider repositoryConfigurationProvider;
        private SecretClient secretClient;
        private GitHubRateLimiter limiter;

        private const string GitHubEventHeader = "X-GitHub-Event";
        public async Task ProcessWebhookAsync(string eventName, string json, ILogger logger, CancellationToken cancellationToken)
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
                            logger.LogWarning("Abuse exception detected. Retry after seconds is: {retrySeconds}",
                                abuseException.RetryAfterSeconds
                                );
                            break;

                        case RateLimitExceededException rateLimitExceededException:
                            retryDelay = rateLimitExceededException.GetRetryAfterTimeSpan();
                            logger.LogWarning(
                                "Rate limit exception detected. Limit is: {limit}, reset is: {reset}, retry seconds is: {retrySeconds}",
                                rateLimitExceededException.Limit,
                                rateLimitExceededException.Reset,
                                retryDelay.TotalSeconds
                                );
                            break;
                    }

                    logger.LogInformation("Waiting for {seconds} before retrying.", retryDelay.TotalSeconds);
                    await Task.Delay(retryDelay);
                })
                .ExecuteAsync(async () =>
                {
                    if (eventName == "check_run")
                    {
                        var handler = new CheckRunHandler(globalConfigurationProvider, gitHubClientProvider, repositoryConfigurationProvider, logger, limiter);
                        await handler.HandleAsync(json, cancellationToken);
                    }
                    else if (eventName == "issue_comment")
                    {
                        var handler = new IssueCommentHandler(globalConfigurationProvider, gitHubClientProvider, repositoryConfigurationProvider, logger, limiter);
                        await handler.HandleAsync(json, cancellationToken);
                    }
                    else if (eventName == "pull_request")
                    {
                        var handler = new PullRequestHandler(globalConfigurationProvider, gitHubClientProvider, repositoryConfigurationProvider, logger, limiter);
                        await handler.HandleAsync(json, cancellationToken);
                    }
                });
        }
    }
}
