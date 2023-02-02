using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Azure.Sdk.Tools.CheckEnforcer.Services.PullRequestTracking;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Octokit;
using YamlDotNet.Core.Events;

namespace Azure.Sdk.Tools.CheckEnforcer.Functions
{
    public class PullRequestTimeoutFunction
    {
        public PullRequestTimeoutFunction(IPullRequestTracker pullRequestTracker, IGitHubClientProvider gitHubClientProvider, GitHubRateLimiter limiter, IRepositoryConfigurationProvider repositoryConfigurationProvider, IGlobalConfigurationProvider globalConfigurationProvider)
        {
            this.pullRequestTracker = pullRequestTracker;
            this.gitHubClientProvider = gitHubClientProvider;
            this.limiter = limiter;
            this.repositoryConfigurationProvider = repositoryConfigurationProvider;
            this.globalConfigurationProvider = globalConfigurationProvider;
        }

        private IPullRequestTracker pullRequestTracker;
        private IGitHubClientProvider gitHubClientProvider;
        private GitHubRateLimiter limiter;
        private IRepositoryConfigurationProvider repositoryConfigurationProvider;
        private IGlobalConfigurationProvider globalConfigurationProvider;

        [FunctionName("PullRequestTimeoutFunction")]
        public async Task Run([TimerTrigger("*/30 * * * * *")]TimerInfo myTimer, ILogger log, CancellationToken cancellationToken)
        {
            log.LogInformation("Fetching tracked pull request tickets.");
            var pullRequestTrackingTickets = await pullRequestTracker.GetTrackedPullRequestsAsync();
            log.LogInformation("Found {ticketCount} pull request tickets.", pullRequestTrackingTickets.Count());

            foreach (var pullRequestTrackingTicket in pullRequestTrackingTickets)
            {
                log.LogInformation(
                    "Processing pull request tracking ticket for installation {installationId} in repository {repositoryId} for pull request number {pullRequestNumber}.",
                    pullRequestTrackingTicket.InstallationId,
                    pullRequestTrackingTicket.RepositoryId,
                    pullRequestTrackingTicket.PullRequestNumber
                    );

                var gitHubClient = await gitHubClientProvider.GetInstallationClientAsync(
                    pullRequestTrackingTicket.InstallationId,
                    cancellationToken
                    );

                await limiter.WaitForGitHubCapacityAsync();
                var pullRequest = await gitHubClient.PullRequest.Get(
                    pullRequestTrackingTicket.RepositoryId,
                    pullRequestTrackingTicket.PullRequestNumber
                    );

                var sha = pullRequest.Head.Sha;
                log.LogInformation(
                    "HEAD SHA for pull request {pullRequestNumber} is {sha}",
                    pullRequestTrackingTicket.PullRequestNumber,
                    sha
                    );

                var configuration = await repositoryConfigurationProvider.GetRepositoryConfigurationAsync(
                    pullRequestTrackingTicket.InstallationId,
                    pullRequestTrackingTicket.RepositoryId,
                    sha,
                    cancellationToken
                    );

                if (configuration.IsEnabled != true)
                {
                    log.LogInformation(
                        "Stopping tracking for pull request {pullRequestNumber} in repository {repositoryId} for installation {installationId} because Check Enforcer is not enabled.",
                        pullRequestTrackingTicket.PullRequestNumber,
                        pullRequestTrackingTicket.RepositoryId,
                        pullRequestTrackingTicket.InstallationId
                        );
                    await pullRequestTracker.StopTrackingPullRequestAsync(pullRequestTrackingTicket);
                    continue;
                }
                else if (pullRequest.State != new StringEnum<ItemState>(ItemState.Open))
                {
                    log.LogInformation(
                        "Stopping tracking for pull request {pullRequestNumber} in repository {repositoryId} for installation {installationId} because it is no longer open.",
                        pullRequestTrackingTicket.PullRequestNumber,
                        pullRequestTrackingTicket.RepositoryId,
                        pullRequestTrackingTicket.InstallationId
                        );
                    await pullRequestTracker.StopTrackingPullRequestAsync(pullRequestTrackingTicket);
                    continue;    
                }
                else if (DateTimeOffset.UtcNow < pullRequest.UpdatedAt.AddMinutes(configuration.TimeoutInMinutes))
                {
                    log.LogInformation(
                        "Skipping pull request {pullRequestNumber} in repository {repositoryId} for installation {installationId} because it is still too new.",
                        pullRequestTrackingTicket.PullRequestNumber,
                        pullRequestTrackingTicket.RepositoryId,
                        pullRequestTrackingTicket.InstallationId
                        );
                    continue;
                }
                else
                {
                    await limiter.WaitForGitHubCapacityAsync();
                    var checkRunRepsonse = await gitHubClient.Check.Run.GetAllForReference(pullRequestTrackingTicket.RepositoryId, sha);
                    
                    if (checkRunRepsonse.TotalCount > 0 && checkRunRepsonse.CheckRuns.All((checkRun) => checkRun.Name == globalConfigurationProvider.GetApplicationName()))
                    {
                        log.LogInformation(
                            "Fetching comments for pull request {pullRequestNumber} in repository {repositoryId} for installation {installationId}.",
                            pullRequestTrackingTicket.PullRequestNumber,
                            pullRequestTrackingTicket.RepositoryId,
                            pullRequestTrackingTicket.InstallationId
                            );

                        await limiter.WaitForGitHubCapacityAsync();
                        var issueComments = await gitHubClient.Issue.Comment.GetAllForIssue(pullRequestTrackingTicket.RepositoryId, pullRequestTrackingTicket.PullRequestNumber);

                        log.LogInformation(
                            "Found {commentCount} on pull request {pullRequestNumber} in repository {repositoryId} for installation {installationId}.",
                            issueComments.Count(),
                            pullRequestTrackingTicket.PullRequestNumber,
                            pullRequestTrackingTicket.RepositoryId,
                            pullRequestTrackingTicket.InstallationId
                            );

                        if (issueComments.Any((comment) => comment.User.Login.StartsWith("check-enforcer")))
                        {
                            log.LogInformation(
                                "Stopping tracking {pullRequestNumber} in repository {repositoryId} for installation {installationId} because it already has help comment.",
                                pullRequestTrackingTicket.PullRequestNumber,
                                pullRequestTrackingTicket.RepositoryId,
                                pullRequestTrackingTicket.InstallationId
                                );
                            await pullRequestTracker.StopTrackingPullRequestAsync(pullRequestTrackingTicket);
                        }
                        else
                        {
                            log.LogInformation(
                                "Adding timeout comment to pull request {pullRequestNumber} in repository {repositoryId} for installation {installationId} because it has no check runs.",
                                pullRequestTrackingTicket.PullRequestNumber,
                                pullRequestTrackingTicket.RepositoryId,
                                pullRequestTrackingTicket.InstallationId
                                );

                            await gitHubClient.Issue.Comment.Create(
                                pullRequestTrackingTicket.RepositoryId,
                                pullRequestTrackingTicket.PullRequestNumber,
                                configuration.Message
                                );

                            await pullRequestTracker.StopTrackingPullRequestAsync(pullRequestTrackingTicket);
                        }

                    }
                    else
                    {
                        log.LogInformation(
                            "Stopping tracking pull request {pullRequestNumber} in repository {repositoryId} for installation{installationId} because it has checks.",
                            pullRequestTrackingTicket.PullRequestNumber,
                            pullRequestTrackingTicket.RepositoryId,
                            pullRequestTrackingTicket.InstallationId
                            );
                        await pullRequestTracker.StopTrackingPullRequestAsync(pullRequestTrackingTicket);
                    }
                }
            }
        }
    }
}
