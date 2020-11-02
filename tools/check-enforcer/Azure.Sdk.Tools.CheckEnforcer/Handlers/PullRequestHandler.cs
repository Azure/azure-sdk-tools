using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Azure.Sdk.Tools.CheckEnforcer.Services.PullRequestTracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Client;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Handlers
{
    public class PullRequestHandler : Handler<PullRequestEventPayload>
    {
        public PullRequestHandler(IGlobalConfigurationProvider globalConfigurationProvider, IGitHubClientProvider gitHubClientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider, ILogger logger, GitHubRateLimiter limiter, IPullRequestTracker pullRequestTracker) : base(globalConfigurationProvider, gitHubClientProvider, repositoryConfigurationProvider, logger, limiter)
        {
            this.pullRequestTracker = pullRequestTracker;
        }

        private IPullRequestTracker pullRequestTracker;

        protected override async Task HandleCoreAsync(HandlerContext<PullRequestEventPayload> context, CancellationToken cancellationToken)
        {
            var payload = context.Payload;
            var installationId = payload.Installation.Id;
            var repositoryId = payload.Repository.Id;
            var sha = payload.PullRequest.Head.Sha;
            var runIdentifier = $"{installationId}/{repositoryId}/{sha}";
            var action = payload.Action;
            var pullRequestNumber = payload.PullRequest.Number;

            Logger.LogInformation(
                "Received {action} action on PR# {pullRequestNumber} against {runIdentifier}",
                action,
                pullRequestNumber,
                runIdentifier
                );

            if (action == "opened" || action == "reopened" || action == "synchronize")
            {
                var configuration = await this.RepositoryConfigurationProvider.GetRepositoryConfigurationAsync(installationId, repositoryId, sha, cancellationToken);

                if (configuration.IsEnabled)
                {
                    var pullRequestTrackingTicket = new PullRequestTrackingTicket(installationId, repositoryId, pullRequestNumber);
                    await pullRequestTracker.StartTrackingPullRequestAsync(pullRequestTrackingTicket);

                    await CreateCheckAsync(context.Client, installationId, repositoryId, sha, false, cancellationToken);

                    if (action == "reopened")
                    {
                        await EvaluatePullRequestAsync(context.Client, installationId, repositoryId, sha, cancellationToken);
                    }
                }
            }
            else
            {
                Logger.LogInformation(
                    "Ignoring pull request event because action was not 'opened' or 'reopened'. It was {action}.",
                    action
                    );
            }

        }
    }
}
