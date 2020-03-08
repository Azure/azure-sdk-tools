using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Azure.Sdk.Tools.CheckEnforcer.Locking;
using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Handlers
{
    public abstract class Handler<T> where T: ActivityPayload
    {
        private const int EventIdBase = 1000;
        private static readonly EventId AcquiringSemaphoreEventId = new EventId(EventIdBase + 0, "Acquring Semaphore");

        public Handler(IGlobalConfigurationProvider globalConfiguratoinProvider, IGitHubClientProvider gitHubClientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider, IDistributedLockProvider distrbutedLockProvider, ILogger logger)
        {
            this.GlobalConfigurationProvider = globalConfiguratoinProvider;
            this.GitHubClientProvider = gitHubClientProvider;
            this.RepositoryConfigurationProvider = repositoryConfigurationProvider;
            this.DistributedLockProvider = distrbutedLockProvider;
            this.Logger = logger;
        }

        protected IGlobalConfigurationProvider GlobalConfigurationProvider { get; private set; }
        protected IGitHubClientProvider GitHubClientProvider { get; private set; }
        protected IRepositoryConfigurationProvider RepositoryConfigurationProvider { get; private set; }
        public IDistributedLockProvider DistributedLockProvider { get; }
        protected ILogger Logger { get; private set; }

        protected async Task SetSuccessAsync(GitHubClient client, long repositoryId, string sha, CancellationToken cancellationToken)
        {
            var response = await client.Check.Run.GetAllForReference(repositoryId, sha);
            var runs = response.CheckRuns;
            var run = runs.Single(r => r.Name == this.GlobalConfigurationProvider.GetApplicationName());

            Logger.LogTrace("Setting check-run to success.");
            await client.Check.Run.Update(repositoryId, run.Id, new CheckRunUpdate()
            {
                Conclusion = new StringEnum<CheckConclusion>(CheckConclusion.Success),
                CompletedAt = DateTimeOffset.UtcNow
            });
            Logger.LogTrace("Set check-run to success.");
        }

        protected async Task SetInProgressAsync(GitHubClient client, long repositoryId, string sha, CancellationToken cancellationToken)
        {
            var response = await client.Check.Run.GetAllForReference(repositoryId, sha);
            var runs = response.CheckRuns;
            var run = runs.Single(r => r.Name == this.GlobalConfigurationProvider.GetApplicationName());

            Logger.LogTrace("Setting check-run to in-progress.");
            await client.Check.Run.Update(repositoryId, run.Id, new CheckRunUpdate()
            {
                Status = new StringEnum<CheckStatus>(CheckStatus.InProgress)
            });
            Logger.LogTrace("Set check-run to in in-progress.");
        }

        protected async Task SetQueuedAsync(GitHubClient client, long repositoryId, string sha, CancellationToken cancellationToken)
        {
            var response = await client.Check.Run.GetAllForReference(repositoryId, sha);
            var runs = response.CheckRuns;
            var run = runs.Single(r => r.Name == this.GlobalConfigurationProvider.GetApplicationName());

            Logger.LogTrace("Setting check-run to queued.");
            await client.Check.Run.Update(repositoryId, run.Id, new CheckRunUpdate()
            {
                Status = new StringEnum<CheckStatus>(CheckStatus.Queued)
            });
            Logger.LogTrace("Set check-run to queued.");
        }

        protected async Task<CheckRun> CreateCheckAsync(GitHubClient client, long repositoryId, string headSha, bool recreate, CancellationToken cancellationToken)
        {
            var response = await client.Check.Run.GetAllForReference(repositoryId, headSha);
            var runs = response.CheckRuns;
            var checkRun = runs.SingleOrDefault(r => r.Name == this.GlobalConfigurationProvider.GetApplicationName());

            if (checkRun == null || recreate)
            {
                Logger.LogTrace("Creating check-run.");

                checkRun = await client.Check.Run.Create(
                    repositoryId,
                    new NewCheckRun(this.GlobalConfigurationProvider.GetApplicationName(), headSha)
                    {
                        Status = new StringEnum<CheckStatus>(CheckStatus.InProgress),
                        StartedAt = DateTimeOffset.UtcNow
                    }
                );

                Logger.LogTrace("Created check-run.");
            }

            return checkRun;
        }

        protected async Task EvaluatePullRequestAsync(GitHubClient client, long installationId, long repositoryId, string sha, CancellationToken cancellationToken)
        {
            var configuration = await this.RepositoryConfigurationProvider.GetRepositoryConfigurationAsync(installationId, repositoryId, sha, cancellationToken);

            var runsResponse = await client.Check.Run.GetAllForReference(repositoryId, sha);
            var runs = runsResponse.CheckRuns;

            var checkEnforcerRun = runs.SingleOrDefault(r => r.Name == this.GlobalConfigurationProvider.GetApplicationName());

            if (checkEnforcerRun == null)
            {
                 Logger.LogTrace("Check-run for enforcer doesn't exist.");
                 return;
            }

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
                Logger.LogTrace("Updating check-run.");
                await client.Check.Run.Update(repositoryId, checkEnforcerRun.Id, new CheckRunUpdate()
                {
                    Conclusion = new StringEnum<CheckConclusion>(CheckConclusion.Success),
                    Status = new StringEnum<CheckStatus>(CheckStatus.Completed),
                    CompletedAt = DateTimeOffset.UtcNow
                });
                Logger.LogTrace("Updated check-run.");
            }
            else if (totalOtherRuns < configuration.MinimumCheckRuns || totalOutstandingOtherRuns != 0 && checkEnforcerRun.Status != new StringEnum<CheckStatus>(CheckStatus.InProgress))
            {
                // NOTE: We do this when we need to go back from a conclusion of success to a status of in-progress.
                await CreateCheckAsync(client, repositoryId, sha, true, cancellationToken);
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
