using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
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
        public IssueCommentHandler(IGlobalConfigurationProvider globalConfigurationProvider, IGitHubClientProvider gitHubClientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider, ILogger logger)
        {
            this.globalConfigurationProvider = globalConfigurationProvider;
            this.gitHubClientProvider = gitHubClientProvider;
            this.repositoryConfigurationProvider = repositoryConfigurationProvider;
            this.logger = logger;
        }

        private IGlobalConfigurationProvider globalConfigurationProvider;
        private IGitHubClientProvider gitHubClientProvider;
        private IRepositoryConfigurationProvider repositoryConfigurationProvider;
        private ILogger logger;

        private async Task SetSuccessAsync(GitHubClient client, long repositoryId, string sha, CancellationToken cancellationToken)
        {
            var response = await client.Check.Run.GetAllForReference(repositoryId, sha);
            var runs = response.CheckRuns;
            var run = runs.Single(r => r.Name == globalConfigurationProvider.GetApplicationName());

            await client.Check.Run.Update(repositoryId, run.Id, new CheckRunUpdate()
            {
                Conclusion = new StringEnum<CheckConclusion>(CheckConclusion.Success)
            });
        }

        private async Task CreateCheckAsync(GitHubClient client, long repositoryId, string sha, CancellationToken cancellationToken)
        {
            var response = await client.Check.Run.GetAllForReference(repositoryId, sha);
            var runs = response.CheckRuns;
            var checkEnforcerRuns = runs.Where(r => r.Name == globalConfigurationProvider.GetApplicationName());

            foreach (var checkEnforcerRun in checkEnforcerRuns)
            {
                await client.Check.Run.Update(repositoryId, checkEnforcerRun.Id, new CheckRunUpdate()
                {
                    Conclusion = new StringEnum<CheckConclusion>(CheckConclusion.Cancelled)
                });
            }

            await client.Check.Run.Create(
                repositoryId,
                new NewCheckRun(globalConfigurationProvider.GetApplicationName(), sha)
                );
        }

        private async Task SetInProgressAsync(GitHubClient client, long repositoryId, string sha, CancellationToken cancellationToken)
        {
            var response = await client.Check.Run.GetAllForReference(repositoryId, sha);
            var runs = response.CheckRuns;
            var run = runs.Single(r => r.Name == globalConfigurationProvider.GetApplicationName());

            await client.Check.Run.Update(repositoryId, run.Id, new CheckRunUpdate()
            {
                Status = new StringEnum<CheckStatus>(CheckStatus.InProgress)
            }); ;
        }

        private async Task SetQueuedAsync(GitHubClient client, long repositoryId, string sha, CancellationToken cancellationToken)
        {
            var response = await client.Check.Run.GetAllForReference(repositoryId, sha);
            var runs = response.CheckRuns;
            var run = runs.Single(r => r.Name == globalConfigurationProvider.GetApplicationName());

            await client.Check.Run.Update(repositoryId, run.Id, new CheckRunUpdate()
            {
                Status = new StringEnum<CheckStatus>(CheckStatus.Queued)
            }); ;
        }

        public override async Task HandleAsync(IssueCommentPayload payload, CancellationToken cancellationToken)
        {
            var installationId = payload.Installation.Id;
            var repositoryId = payload.Repository.Id;
            var comment = payload.Comment.Body.ToLower();
            var issueId = payload.Issue.Number;

            var client = await gitHubClientProvider.GetInstallationClientAsync(installationId, cancellationToken);
            var pullRequest = await client.PullRequest.Get(repositoryId, issueId);
            var sha = pullRequest.Head.Sha;

            var configuration = await repositoryConfigurationProvider.GetRepositoryConfigurationAsync(installationId, repositoryId, sha, cancellationToken);
            
            switch (comment)
            {
                case "/check-enforcer queued":
                    await SetQueuedAsync(client, repositoryId, sha, cancellationToken);
                    break;

                case "/check-enforcer inprogress":
                    await SetInProgressAsync(client, repositoryId, sha, cancellationToken);
                    break;

                case "/check-enforcer success":
                    await SetSuccessAsync(client, repositoryId, sha, cancellationToken);
                    break;

                case "/check-enforcer reset":
                    await CreateCheckAsync(client, repositoryId, sha, cancellationToken);
                    break;

                default:
                    logger.LogTrace("Unrecognized command: {comment}", comment);
                    break;
            }
        }
    }
}
