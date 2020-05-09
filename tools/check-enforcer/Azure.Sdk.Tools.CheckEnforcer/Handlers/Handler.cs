using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
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
            var checkEnforcerRuns = runs.Where(r => r.Name == this.GlobalConfigurationProvider.GetApplicationName());

            Logger.LogInformation(
                "Found {count} Check Enforcer runs on sha {commitSha} in repository {repository}.",
                checkEnforcerRuns.Count(),
                sha,
                repositoryId
                );

            foreach (var checkEnforcerRun in checkEnforcerRuns)
            {
                Logger.LogInformation("Setting check-run {checkEnforcerRunId} to success.", checkEnforcerRun.Id);
                await client.Check.Run.Update(repositoryId, checkEnforcerRun.Id, new CheckRunUpdate()
                {
                    Conclusion = new StringEnum<CheckConclusion>(CheckConclusion.Success),
                    CompletedAt = DateTimeOffset.UtcNow
                });
                Logger.LogInformation("Set check-run {checkEnforcerRunId} to success.", checkEnforcerRun.Id);
            }
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

        protected async Task<CheckRun> CreateCheckAsync(GitHubClient client, long installationId, long repositoryId, string headSha, bool recreate, CancellationToken cancellationToken)
        {
            var runIdentifier = $"{installationId}/{repositoryId}/{headSha}";

            Logger.LogInformation("Checking for existing check-run for: {runIdentifier}", runIdentifier);

            var response = await client.Check.Run.GetAllForReference(repositoryId, headSha);
            var runs = response.CheckRuns;
            var checkRun = runs.SingleOrDefault(r => r.Name == this.GlobalConfigurationProvider.GetApplicationName());

            if (checkRun == null || recreate)
            {
                Logger.LogInformation("Creating check-run for: {runIdentifier}", runIdentifier);

                checkRun = await client.Check.Run.Create(
                    repositoryId,
                    new NewCheckRun(this.GlobalConfigurationProvider.GetApplicationName(), headSha)
                    {
                        Status = new StringEnum<CheckStatus>(CheckStatus.InProgress),
                        StartedAt = DateTimeOffset.UtcNow
                    }
                );

                Logger.LogInformation("Created check-run for: {runIdentifier}", runIdentifier);
            }

            return checkRun;
        }

        protected async Task EvaluatePullRequestAsync(GitHubClient client, long installationId, long repositoryId, string sha, CancellationToken cancellationToken)
        {
            var runIdentifier = $"{installationId}/{repositoryId}/{sha}";

            var configuration = await this.RepositoryConfigurationProvider.GetRepositoryConfigurationAsync(installationId, repositoryId, sha, cancellationToken);

            Logger.LogInformation("Fetching check-runs for: {runIdentifier} for evaluation.", runIdentifier);

            var runsResponse = await client.Check.Run.GetAllForReference(repositoryId, sha);
            var runs = runsResponse.CheckRuns;
            var checkEnforcerRuns = runs.Where(r => r.Name == this.GlobalConfigurationProvider.GetApplicationName());

            Logger.LogInformation("Check-suite for: {runIdentifier} has {runs.Count} check-enforcer runs (possible race condition?).", runIdentifier, checkEnforcerRuns.Count());

            foreach (var duplicatedCheckEnforcerRun in checkEnforcerRuns)
            {
                Logger.LogInformation(
                    "One check-enforcer run for {runIdentifier} targets SHA {sha} at URL {url}.",
                    runIdentifier,
                    duplicatedCheckEnforcerRun.HeadSha,
                    duplicatedCheckEnforcerRun.HtmlUrl
                    );
            }

            var otherRuns = from run in runs
                            where run.Name != this.GlobalConfigurationProvider.GetApplicationName()
                            select run;

            var totalOtherRuns = otherRuns.Count();

            var outstandingOtherRuns = from run in otherRuns
                                        where run.Conclusion != new StringEnum<CheckConclusion>(CheckConclusion.Success)
                                        select run;

            var totalOutstandingOtherRuns = outstandingOtherRuns.Count();

            if (totalOtherRuns >= configuration.MinimumCheckRuns && totalOutstandingOtherRuns == 0 && checkEnforcerRuns.Any(checkEnforcerRun => checkEnforcerRun.Conclusion != new StringEnum<CheckConclusion>(CheckConclusion.Success)))
            {
                foreach (var checkEnforcerRun in checkEnforcerRuns)
                {
                    Logger.LogInformation("Updating check-run for: {runIdentifier}", runIdentifier);
                    await client.Check.Run.Update(repositoryId, checkEnforcerRun.Id, new CheckRunUpdate()
                    {
                        Conclusion = new StringEnum<CheckConclusion>(CheckConclusion.Success),
                        Status = new StringEnum<CheckStatus>(CheckStatus.Completed),
                        CompletedAt = DateTimeOffset.UtcNow
                    });
                    Logger.LogInformation("Updated check-run for: {runIdentifier}", runIdentifier);
                }

            }
        }

        private T DeserializePayload(string json)
        {
            Logger.LogInformation("Payload: {json}", json);

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
