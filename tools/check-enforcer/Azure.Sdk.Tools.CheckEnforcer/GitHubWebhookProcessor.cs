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
        public GitHubWebhookProcessor(ILogger log, GitHubClientFactory gitHubClientFactory, ConfigurationStore configurationStore)
        {
            this.Log = log;
            this.ClientFactory = gitHubClientFactory;
            this.ConfigurationStore = configurationStore;
        }

        public ILogger Log { get; private set; }

        public GitHubClientFactory ClientFactory { get; private set; }

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

        private async Task<CheckRun> EnsureCheckEnforcerRunAsync(GitHubClient client, long repositoryId, string headSha, IReadOnlyList<CheckRun> runs, bool recreate)
        {
            var checkRun = runs.SingleOrDefault(r => r.Name == Constants.ApplicationName);

            if (checkRun == null || recreate)
            {
                Log.LogDebug("Creating Check Enforcer run.");
                checkRun = await client.Check.Run.Create(
                    repositoryId,
                    new NewCheckRun(Constants.ApplicationName, headSha)
                );
            }

            return checkRun;
        }

        private async Task EvaluateAndUpdateCheckEnforcerRunStatusAsync(IRepositoryConfiguration configuration, long installationId, long repositoryId, string pullRequestSha, CancellationToken cancellationToken)
        {
            Log.LogDebug("Fetching installation client.");
            var client = await this.ClientFactory.GetInstallationClientAsync(installationId, cancellationToken);

            Log.LogDebug("Fetching check runs.");
            var runsResponse = await client.Check.Run.GetAllForReference(repositoryId, pullRequestSha);
            var runs = runsResponse.CheckRuns;

            var checkEnforcerRun = await EnsureCheckEnforcerRunAsync(client, repositoryId, pullRequestSha, runs, false);

            var otherRuns = from run in runs
                            where run.Name != Constants.ApplicationName
                            select run;

            var totalOtherRuns = otherRuns.Count();


            var outstandingOtherRuns = from run in otherRuns
                                       where run.Conclusion != new StringEnum<CheckConclusion>(CheckConclusion.Success)
                                       select run;


            var totalOutstandingOtherRuns = outstandingOtherRuns.Count();

            Log.LogDebug("{totalOutstandingOtherRuns}/{totalOtherRuns} other runs outstanding.", totalOutstandingOtherRuns, totalOtherRuns);

            if (totalOtherRuns >= configuration.MinimumCheckRuns && totalOutstandingOtherRuns == 0)
            {
                Log.LogDebug("Check Enforcer criteria met, marking check successful.");
                await client.Check.Run.Update(repositoryId, checkEnforcerRun.Id, new CheckRunUpdate()
                {
                    Conclusion = new StringEnum<CheckConclusion>(CheckConclusion.Success)
                });
            }
            else
            {
                if (checkEnforcerRun.Conclusion == new StringEnum<CheckConclusion>(CheckConclusion.Success) && totalOutstandingOtherRuns > 0)
                {
                    Log.LogDebug("Check Enforcer run was previously marked successful, but there are now outstanding runs. Recreating check.");
                    await EnsureCheckEnforcerRunAsync(client, repositoryId, pullRequestSha, runs, true);
                }
                else
                {
                    if (checkEnforcerRun.Status != new StringEnum<CheckStatus>(CheckStatus.InProgress))
                    {
                        Log.LogDebug("Updating Check Enforcer status to in-progress.");
                        await client.Check.Run.Update(repositoryId, checkEnforcerRun.Id, new CheckRunUpdate()
                        {
                            Status = new StringEnum<CheckStatus>(CheckStatus.InProgress)
                        });
                    }
                }

            }
        }

        public async Task ProcessWebhookAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            if (request.Headers.TryGetValue(GitHubEventHeader, out StringValues eventName))
            {
                if (eventName == "check_run")
                {
                    var rawPayload = request.Body;
                    var payload = await DeserializePayloadAsync<CheckRunEventPayload>(rawPayload);

                    var installationId = payload.Installation.Id;
                    var repositoryId = payload.Repository.Id;
                    var pullRequestSha = payload.CheckRun.CheckSuite.HeadSha;

                    Log.LogDebug("Fetching repository configuration.");
                    var configuration = await this.ConfigurationStore.GetRepositoryConfigurationAsync(installationId, repositoryId, pullRequestSha, cancellationToken);
                    Log.LogDebug("Repository configuration: {configuration}", configuration.ToString());

                    if (configuration.IsEnabled)
                    {
                        Log.LogDebug("Repository was enabled for Check Enforcer.",
                            installationId,
                            repositoryId,
                            pullRequestSha
                            );
                        await EvaluateAndUpdateCheckEnforcerRunStatusAsync(configuration, installationId, repositoryId, pullRequestSha, cancellationToken);
                    }
                    else
                    {
                        Log.LogInformation("Repository was not enabled for Check Enforcer.");
                    }
                }
                else if (eventName == "check_suite")
                {
                    // HACK: We swallow check_suite events. Technically we could register
                    //       the check enforcer check run earlier (before the PR is even created)
                    //       but at this point we don't know the target branch for sure so we
                    //       can't potentially cache the configuration entry.
                    //
                    //       However - we can't opt out of receiving this event even then check suite
                    //       is unchecked in the app's event setup. So rather than returning a 400
                    //       for this event we just swallow it to eliminate the noise.
                    return;
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
