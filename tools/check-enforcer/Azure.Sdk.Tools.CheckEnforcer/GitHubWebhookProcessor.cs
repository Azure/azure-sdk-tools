using Azure.Core;
using Azure.Identity;
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
        public GitHubWebhookProcessor(ILogger log, GitHubClientFactory gitHubClientFactory, ConfigurationStore configurationStore, GlobalConfiguration globalConfiguration)
        {
            this.Log = log;
            this.ClientFactory = gitHubClientFactory;
            this.GlobalConfiguration = globalConfiguration;
            this.ConfigurationStore = configurationStore;
        }

        public ILogger Log { get; private set; }

        public GitHubClientFactory ClientFactory { get; private set; }
        public GlobalConfiguration GlobalConfiguration { get; private set; }
        public ConfigurationStore ConfigurationStore { get; private set; }

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
            var client = await this.ClientFactory.GetInstallationClientAsync(installationId, cancellationToken);

            var runsResponse = await client.Check.Run.GetAllForReference(repositoryId, pullRequestSha);
            var runs = runsResponse.CheckRuns;

            // NOTE: If this blows up it means that we didn't receive the check_suite request.
            var checkEnforcerRun = runs.Single(r => r.Name == GlobalConfiguration.GetApplicationID());

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
            // HACK: Removing this as I think it could be introducing a consistency issue. THis means that if
            //       someone gets check-enforcer to pass then it is always passed, even if the pipelines are
            //       requeued (less than ideal).
            //
            //else if (checkEnforcerRun.Conclusion == new StringEnum<CheckConclusion>(CheckConclusion.Success))
            //{
            //    // NOTE: We do this when we need to go back from a conclusion of success to a status of in-progress.
            //    await CreateCheckEnforcerRunAsync(client, repositoryId, pullRequestSha, true);
            //}
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

                    // Extract critical info for payload.
                    installationId = payload.Installation.Id;
                    repositoryId = payload.Repository.Id;
                    pullRequestSha = payload.CheckRun.CheckSuite.HeadSha;

                    var configuration = await ConfigurationStore.GetRepositoryConfigurationAsync(
                        installationId, repositoryId, pullRequestSha, cancellationToken
                        );

                    if (configuration.IsEnabled)
                    {
                        await EvaluateAndUpdateCheckEnforcerRunStatusAsync(
                            configuration, installationId, repositoryId, pullRequestSha, cancellationToken
                            );
                    }
                }
                else if (eventName == "check_suite")
                {
                    var rawPayload = request.Body;
                    var payload = await DeserializePayloadAsync<CheckSuiteEventPayload>(rawPayload);

                    // Extract critical info for payload.
                    installationId = payload.Installation.Id;
                    repositoryId = payload.Repository.Id;
                    pullRequestSha = payload.CheckSuite.HeadSha;

                    var client = await ClientFactory.GetInstallationClientAsync(installationId, cancellationToken);
                    await CreateCheckEnforcerRunAsync(client, repositoryId, pullRequestSha, true);
                    return;
                }
                else if (eventName == "issue_comment")
                {
                    var rawPayload = request.Body;
                    var payload = await DeserializePayloadAsync<IssueCommentPayload>(rawPayload);
                    var comment = payload.Comment.Body.ToLower();

                    if (payload.Action == "created" && payload.Comment.Body.ToLower() == "/check-enforcer evaluate")
                    {
                        installationId = payload.Installation.Id;
                        repositoryId = payload.Repository.Id;

                        var client = await ClientFactory.GetInstallationClientAsync(installationId, cancellationToken);
                        var pullRequest = await client.PullRequest.Get(repositoryId, payload.Issue.Number);
                        pullRequestSha = pullRequest.Head.Sha;

                        var configuration = await this.ConfigurationStore.GetRepositoryConfigurationAsync(installationId, repositoryId, pullRequestSha, cancellationToken);

                        if (configuration.IsEnabled)
                        {
                            await EvaluateAndUpdateCheckEnforcerRunStatusAsync(configuration, installationId, repositoryId, pullRequestSha, cancellationToken);
                        }
                    }
                    else
                    {
                        return;
                    }
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
