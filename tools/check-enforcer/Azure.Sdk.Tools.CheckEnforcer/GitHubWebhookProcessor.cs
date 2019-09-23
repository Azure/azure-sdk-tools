using Azure.Core;
using Azure.Identity;
using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Handlers;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Octokit;
using Octokit.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class GitHubWebhookProcessor
    {
        public GitHubWebhookProcessor(ILogger log, IGitHubClientProvider gitHubClientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider, GlobalConfiguration globalConfiguration)
        {
            this.Log = log;
            this.GlobalConfiguration = globalConfiguration;
            this.gitHubClientProvider = gitHubClientProvider;
            this.repositoryConfigurationProvider = repositoryConfigurationProvider;
        }

        public ILogger Log { get; private set; }

        public IGitHubClientProvider gitHubClientProvider;
        public GlobalConfiguration GlobalConfiguration { get; private set; }
        private IRepositoryConfigurationProvider repositoryConfigurationProvider;

        private const string GitHubEventHeader = "X-GitHub-Event";

        private async Task<TEvent> DeserializePayloadAsync<TEvent>(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                var rawPayload = await reader.ReadToEndAsync();
                Log.LogInformation("Received payload from GitHub: {rawPayload}", rawPayload);

                var serializer = new SimpleJsonSerializer();
                var payload = serializer.Deserialize<TEvent>(rawPayload);

                return payload;
            }
        }

        private async Task<CheckRun> CreateCheckEnforcerRunAsync(GitHubClient client, long repositoryId, string headSha, bool recreate)
        {
            var response = await client.Check.Run.GetAllForReference(repositoryId, headSha);
            var runs = response.CheckRuns;
            var checkRun = runs.SingleOrDefault(r => r.Name == this.GlobalConfiguration.GetApplicationName());

            if (checkRun == null || recreate)
            {
                checkRun = await client.Check.Run.Create(
                    repositoryId,
                    new NewCheckRun(this.GlobalConfiguration.GetApplicationName(), headSha)
                    {
                        Status = new StringEnum<CheckStatus>(CheckStatus.InProgress)
                    }
                );
            }

            return checkRun;
        }

        private async Task EvaluateAndUpdateCheckEnforcerRunStatusAsync(IRepositoryConfiguration configuration, long installationId, long repositoryId, string pullRequestSha, CancellationToken cancellationToken)
        {
            var client = await gitHubClientProvider.GetInstallationClientAsync(installationId, cancellationToken);

            var runsResponse = await client.Check.Run.GetAllForReference(repositoryId, pullRequestSha);
            var runs = runsResponse.CheckRuns;

            // NOTE: If this blows up it means that we didn't receive the check_suite request.
            var checkEnforcerRun = await CreateCheckEnforcerRunAsync(client, repositoryId, pullRequestSha, false);

            var otherRuns = from run in runs
                            where run.Name != this.GlobalConfiguration.GetApplicationName()
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
                    Status = new StringEnum<CheckStatus>(CheckStatus.Completed)
                });
            }
            else if (checkEnforcerRun.Conclusion == new StringEnum<CheckConclusion>(CheckConclusion.Success))
            {
                // NOTE: We do this when we need to go back from a conclusion of success to a status of in-progress.
                await CreateCheckEnforcerRunAsync(client, repositoryId, pullRequestSha, true);
            }
        }

        public async Task ProcessWebhookAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            if (request.Headers.TryGetValue(GitHubEventHeader, out StringValues eventName))
            {
                long installationId;
                long repositoryId;
                string pullRequestSha;


                if (eventName == "check_run")
                {
                    var rawPayload = request.Body;
                    var payload = await DeserializePayloadAsync<CheckRunEventPayload>(rawPayload);

                    if (payload.CheckRun.Name != GlobalConfiguration.GetApplicationName())
                    {
                        // Extract critical info for payload.
                        installationId = payload.Installation.Id;
                        repositoryId = payload.Repository.Id;
                        pullRequestSha = payload.CheckRun.CheckSuite.HeadSha;

                        var configuration = await repositoryConfigurationProvider.GetRepositoryConfigurationAsync(
                            installationId, repositoryId, pullRequestSha, cancellationToken
                            );

                        if (configuration.IsEnabled)
                        {
                            await EvaluateAndUpdateCheckEnforcerRunStatusAsync(
                                configuration, installationId, repositoryId, pullRequestSha, cancellationToken
                                );
                        }
                    }
                }
                else if (eventName == "check_suite")
                {
                    Log.LogWarning("We got here!");
                    var rawPayload = request.Body;
                    var payload = await DeserializePayloadAsync<CheckSuiteEventPayload>(rawPayload);

                    if (payload.Action == "requested" || payload.Action == "rerequested")
                    {
                        // Extract critical info for payload.
                        installationId = payload.Installation.Id;
                        repositoryId = payload.Repository.Id;
                        pullRequestSha = payload.CheckSuite.HeadSha;

                        var client = await gitHubClientProvider.GetInstallationClientAsync(installationId, cancellationToken);
                        await CreateCheckEnforcerRunAsync(client, repositoryId, pullRequestSha, true);
                    }
                    return;
                }
                else if (eventName == "issue_comment")
                {
                    var rawPayload = request.Body;
                    var payload = await DeserializePayloadAsync<IssueCommentPayload>(rawPayload);

                    var handler = new IssueCommentHandler(repositoryConfigurationProvider, gitHubClientProvider, Log);
                    await handler.HandleAsync(payload, cancellationToken);

                }
                else
                {
                    throw new CheckEnforcerUnsupportedEventException(eventName);
                }
            }
            else
            {
                throw new CheckEnforcerException($"Could not find header '{GitHubEventHeader}'.");
            }
        }
    }
}
