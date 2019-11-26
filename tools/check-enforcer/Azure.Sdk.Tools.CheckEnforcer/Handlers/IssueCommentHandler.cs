using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Azure.Sdk.Tools.CheckEnforcer.Locking;
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
    public class IssueCommentHandler : Handler<IssueCommentPayload>
    {
        public IssueCommentHandler(IGlobalConfigurationProvider globalConfigurationProvider, IGitHubClientProvider gitHubClientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider, IDistributedLockProvider distributedLockProvider, ILogger logger) : base(globalConfigurationProvider, gitHubClientProvider, repositoryConfigurationProvider, distributedLockProvider, logger)
        {
        }

        protected override async Task HandleCoreAsync(HandlerContext<IssueCommentPayload> context, CancellationToken cancellationToken)
        {
            var payload = context.Payload;
            var installationId = payload.Installation.Id;
            var repositoryId = payload.Repository.Id;
            var comment = payload.Comment.Body.ToLower();
            var issueId = payload.Issue.Number;

            // Bail early if we aren't even a check enforcer comment. Reduces exception noise.
            if (!comment.StartsWith("/check-enforcer")) return;

            var pullRequest = await context.Client.PullRequest.Get(repositoryId, issueId);
            var sha = pullRequest.Head.Sha;

            var distributedLockIdentifier = $"{installationId}/{repositoryId}/{sha}";

            using (var distributedLock = DistributedLockProvider.Create(distributedLockIdentifier))
            {
                var distributedLockAcquired = await distributedLock.AcquireAsync();
                if (!distributedLockAcquired) return;

                switch (comment)
                {
                    case "/check-enforcer queued":
                        await SetQueuedAsync(context.Client, repositoryId, sha, cancellationToken);
                        break;

                    case "/check-enforcer inprogress":
                        await SetInProgressAsync(context.Client, repositoryId, sha, cancellationToken);
                        break;

                    case "/check-enforcer success":
                        await SetSuccessAsync(context.Client, repositoryId, sha, cancellationToken);
                        break;

                    case "/check-enforcer reset":
                        await CreateCheckAsync(context.Client, repositoryId, sha, true, cancellationToken);
                        break;

                    case "/check-enforcer evaluate":
                        await EvaluatePullRequestAsync(context.Client, installationId, repositoryId, sha, cancellationToken);
                        break;

                    default:
                        this.Logger.LogTrace("Unrecognized command: {comment}", comment);
                        break;
                }
                await distributedLock.ReleaseAsync();
            }
        }
    }
}
