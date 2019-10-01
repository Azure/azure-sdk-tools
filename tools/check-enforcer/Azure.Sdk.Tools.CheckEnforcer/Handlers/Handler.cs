using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Handlers
{
    public abstract class Handler<T> where T: ActivityPayload
    {
        public Handler(IGlobalConfigurationProvider globalConfiguratoinProvider, IGitHubClientProvider gitHubClientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider, ILogger logger)
        {
            this.GlobalConfigurationProvider = globalConfiguratoinProvider;
            this.GitHubClientProvider = gitHubClientProvider;
            this.RepositoryConfigurationProvider = repositoryConfigurationProvider;
            this.Logger = logger;
        }

        protected IGlobalConfigurationProvider GlobalConfigurationProvider { get; private set; }
        protected IGitHubClientProvider GitHubClientProvider { get; private set; }
        protected IRepositoryConfigurationProvider RepositoryConfigurationProvider { get; private set; }


        protected ILogger Logger { get; private set; }

        protected async Task SetSuccessAsync(GitHubClient client, long repositoryId, string sha, CancellationToken cancellationToken)
        {
            var response = await client.Check.Run.GetAllForReference(repositoryId, sha);
            var runs = response.CheckRuns;
            var run = runs.Single(r => r.Name == this.GlobalConfigurationProvider.GetApplicationName());

            await client.Check.Run.Update(repositoryId, run.Id, new CheckRunUpdate()
            {
                Conclusion = new StringEnum<CheckConclusion>(CheckConclusion.Success),
                CompletedAt = DateTimeOffset.UtcNow
            });
        }

        protected async Task SetInProgressAsync(GitHubClient client, long repositoryId, string sha, CancellationToken cancellationToken)
        {
            var response = await client.Check.Run.GetAllForReference(repositoryId, sha);
            var runs = response.CheckRuns;
            var run = runs.Single(r => r.Name == this.GlobalConfigurationProvider.GetApplicationName());

            await client.Check.Run.Update(repositoryId, run.Id, new CheckRunUpdate()
            {
                Status = new StringEnum<CheckStatus>(CheckStatus.InProgress)
            });
        }

        protected async Task SetQueuedAsync(GitHubClient client, long repositoryId, string sha, CancellationToken cancellationToken)
        {
            var response = await client.Check.Run.GetAllForReference(repositoryId, sha);
            var runs = response.CheckRuns;
            var run = runs.Single(r => r.Name == this.GlobalConfigurationProvider.GetApplicationName());

            await client.Check.Run.Update(repositoryId, run.Id, new CheckRunUpdate()
            {
                Status = new StringEnum<CheckStatus>(CheckStatus.Queued)
            });
        }

        protected async Task<CheckRun> CreateCheckAsync(GitHubClient client, long repositoryId, string headSha, bool recreate, CancellationToken cancellationToken)
        {
            var response = await client.Check.Run.GetAllForReference(repositoryId, headSha);
            var runs = response.CheckRuns;
            var checkEnforcerRuns = runs.Where(r => r.Name == this.GlobalConfigurationProvider.GetApplicationName());

            if (checkEnforcerRuns.Count() > 1)
            {
                var firstRun = checkEnforcerRuns.First();
                SimpleJsonSerializer serializer = new SimpleJsonSerializer();
                var serializedFirstRun = serializer.Serialize(firstRun);

                Logger.LogTrace("Duplicated run: {serializedFirstRun}", serializedFirstRun);
            }

            var checkRun = checkEnforcerRuns.SingleOrDefault();

            if (checkRun == null || recreate)
            {
                checkRun = await client.Check.Run.Create(
                    repositoryId,
                    new NewCheckRun(this.GlobalConfigurationProvider.GetApplicationName(), headSha)
                    {
                        Status = new StringEnum<CheckStatus>(CheckStatus.InProgress),
                        StartedAt = DateTimeOffset.UtcNow
                    }
                );
            }

            return checkRun;
        }

        protected async Task EvaluatePullRequestAsync(GitHubClient client, long installationId, long repositoryId, string sha, CancellationToken cancellationToken)
        {
            var configuration = await this.RepositoryConfigurationProvider.GetRepositoryConfigurationAsync(installationId, repositoryId, sha, cancellationToken);

            if (configuration.IsEnabled)
            {
                var runsResponse = await client.Check.Run.GetAllForReference(repositoryId, sha);
                var runs = runsResponse.CheckRuns;

                // NOTE: If this blows up it means that we didn't receive the check_suite request.
                var checkEnforcerRun = await CreateCheckAsync(client, repositoryId, sha, false, cancellationToken);

                var otherRuns = from run in runs
                                where run.Name != this.GlobalConfigurationProvider.GetApplicationName()
                                select run;

                var totalOtherRuns = otherRuns.Count();

                var outstandingOtherRuns = from run in otherRuns
                                           where run.Conclusion != new StringEnum<CheckConclusion>(CheckConclusion.Success)
                                           select run;

                var totalOutstandingOtherRuns = outstandingOtherRuns.Count();

                if (totalOtherRuns >= configuration.MinimumCheckRuns && totalOutstandingOtherRuns == 0 && checkEnforcerRun.Conclusion != new StringEnum<CheckConclusion>(CheckConclusion.Success))
                {
                    await client.Check.Run.Update(repositoryId, checkEnforcerRun.Id, new CheckRunUpdate()
                    {
                        Conclusion = new StringEnum<CheckConclusion>(CheckConclusion.Success),
                        Status = new StringEnum<CheckStatus>(CheckStatus.Completed),
                        CompletedAt = DateTimeOffset.UtcNow
                    });
                }
                else if (checkEnforcerRun.Conclusion == new StringEnum<CheckConclusion>(CheckConclusion.Success))
                {
                    // NOTE: We do this when we need to go back from a conclusion of success to a status of in-progress.
                    await CreateCheckAsync(client, repositoryId, sha, true, cancellationToken);
                }
            }
        }

        private T DeserializePayload(string json)
        {
            Logger.LogTrace("Payload: {json}", json);

            SimpleJsonSerializer serializer = new SimpleJsonSerializer();
            var payload = serializer.Deserialize<T>(json);
            return payload;
        }

        public async Task HandleAsync(string json, CancellationToken cancellationToken)
        {
            var deserializedPayload = DeserializePayload(json);
            var installationId = deserializedPayload.Installation.Id;

            var client = await this.GitHubClientProvider.GetInstallationClientAsync(installationId, cancellationToken);
            var context = new HandlerContext<T>(deserializedPayload, client);

            await HandleCoreAsync(context, cancellationToken);
        }

        protected abstract Task HandleCoreAsync(HandlerContext<T> context, CancellationToken cancellationToken);
    }
}
