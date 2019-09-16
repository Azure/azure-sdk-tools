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
        public GitHubWebhookProcessor(GitHubClientFactory gitHubClientFactory, ConfigurationStore configurationStore)
        {
            this.ClientFactory = gitHubClientFactory;
            this.ConfigurationStore = configurationStore;
        }


        public GitHubClientFactory ClientFactory { get; private set; }

        public ConfigurationStore ConfigurationStore { get; private set; }

        private const string GitHubEventHeader = "X-GitHub-Event";

        private async Task<TEvent> DeserializePayloadAsync<TEvent>(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                var payloadString = await reader.ReadToEndAsync();
                var serializer = new SimpleJsonSerializer();
                var payload = serializer.Deserialize<TEvent>(payloadString);
                return payload;
            }
        }

        private async Task UpdateCheckRunAsync(long repositoryId, GitHubClient installationClient, IEnumerable<CheckRun> runs, CheckRun run)
        {
            var oustandingChecksCount = (from cr in runs
                                         where cr.Name != Constants.ApplicationName
                                         where cr.Conclusion.Value != new StringEnum<CheckConclusion>(CheckConclusion.Success)
                                         select cr).Count();

            if (oustandingChecksCount > 0)
            {
                await installationClient.Check.Run.Update(repositoryId, run.Id, new CheckRunUpdate()
                {
                    Status = new StringEnum<CheckStatus>(CheckStatus.InProgress)
                });
            }
            else
            {
                await installationClient.Check.Run.Update(repositoryId, run.Id, new CheckRunUpdate()
                {
                    Conclusion = new StringEnum<CheckConclusion>(CheckConclusion.Success)
                });
            }
        }

        private async Task CreateCheckRunAsync(CheckSuiteEventPayload payload, long repositoryId, GitHubClient installationClient)
        {
            var checkRun = await installationClient.Check.Run.Create(
                repositoryId,
                new NewCheckRun(Constants.ApplicationName, payload.CheckSuite.HeadSha)
            );

            checkRun = await installationClient.Check.Run.Update(repositoryId, checkRun.Id, new CheckRunUpdate()
            {
                Status = new StringEnum<CheckStatus>(CheckStatus.InProgress)
            });
        }

        private async Task<CheckRun> EnsureCheckEnforcerRunAsync(GitHubClient client, long repositoryId, string headSha, IReadOnlyList<CheckRun> runs, bool recreate)
        {
            var checkRun = runs.SingleOrDefault(r => r.Name == Constants.ApplicationName);

            if (checkRun == null || recreate)
            {
                checkRun = await client.Check.Run.Create(
                    repositoryId,
                    new NewCheckRun(Constants.ApplicationName, headSha)
                );
            }

            return checkRun;
        }

        private async Task EvaluateAndUpdateCheckEnforcerRunStatusAsync(RepositoryConfiguration configuration, long installationId, long repositoryId, string headSha, CancellationToken cancellationToken)
        {
            var client = await this.ClientFactory.GetInstallationClientAsync(installationId, cancellationToken);
            var runsResponse = await client.Check.Run.GetAllForReference(repositoryId, headSha);
            var runs = runsResponse.CheckRuns;

            var checkEnforcerRun = await EnsureCheckEnforcerRunAsync(client, repositoryId, headSha, runs, false);

            var otherRuns = from run in runs
                            where run.Name != Constants.ApplicationName
                            select run;

            var totalOtherRuns = otherRuns.Count();

            var outstandingOtherRuns = from run in otherRuns
                                       where run.Conclusion != new StringEnum<CheckConclusion>(CheckConclusion.Success)
                                       select run;

            var totalOutstandingOtherRuns = outstandingOtherRuns.Count();

            if (totalOtherRuns >= configuration.MinimumCheckRuns && totalOutstandingOtherRuns == 0)
            {
                await client.Check.Run.Update(repositoryId, checkEnforcerRun.Id, new CheckRunUpdate()
                {
                    Conclusion = new StringEnum<CheckConclusion>(CheckConclusion.Success)
                });
            }
            else
            {
                if (checkEnforcerRun.Conclusion == new StringEnum<CheckConclusion>(CheckConclusion.Success) && totalOutstandingOtherRuns > 0)
                {
                    await EnsureCheckEnforcerRunAsync(client, repositoryId, headSha, runs, true);
                }
                else
                {
                    if (checkEnforcerRun.Status != new StringEnum<CheckStatus>(CheckStatus.InProgress))
                    {
                        await client.Check.Run.Update(repositoryId, checkEnforcerRun.Id, new CheckRunUpdate()
                        {
                            Status = new StringEnum<CheckStatus>(CheckStatus.InProgress)
                        });
                    }
                }

            }
        }

        public async Task ProcessWebhookAsync(HttpRequest request, ILogger log, CancellationToken cancellationToken)
        {
            if (request.Headers.TryGetValue(GitHubEventHeader, out StringValues eventName))
            {
                long installationId;
                long repositoryId;
                string headSha;

                // TODO: Consider something more elegant here.
                if (eventName == "check_suite" )
                {
                    var payload = await DeserializePayloadAsync<CheckSuiteEventPayload>(request.Body);
                    installationId = payload.Installation.Id;
                    repositoryId = payload.Repository.Id;
                    headSha = payload.CheckSuite.HeadSha;
                }
                else if (eventName == "check_run")
                {
                    var payload = await DeserializePayloadAsync<CheckRunEventPayload>(request.Body);
                    installationId = payload.Installation.Id;
                    repositoryId = payload.Repository.Id;
                    headSha = payload.CheckRun.CheckSuite.HeadSha;
                }
                else
                {
                    throw new CheckEnforcerUnsupportedEventException(eventName);
                }

                var configuration = await this.ConfigurationStore.GetRepositoryConfigurationAsync(installationId, repositoryId, cancellationToken);

                if (configuration.IsEnabled)
                {
                    await EvaluateAndUpdateCheckEnforcerRunStatusAsync(configuration, installationId, repositoryId, headSha, cancellationToken);
                }
            }
            else
            {
                throw new CheckEnforcerException($"Could not find header '{GitHubEventHeader}'.");
            }
        }
    }
}
