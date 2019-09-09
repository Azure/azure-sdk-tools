using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Primitives;
using System.Reflection.Metadata.Ecma335;
using Octokit;
using Octokit.Internal;
using GitHubJwt;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHubJwt;
using System.Collections;
using System.Collections.Generic;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public static class GitHubWebhookFunction
    {
        private const string ApplicationName = "check-enforcer";
        private const string GitHubEventHeader = "X-GitHub-Event";

        private static IPrivateKeySource GetPrivateKeySource()
        {
            var privateKeySource = new KeyVaultPrivateKeySource();
            return privateKeySource;
        }

        private static int GetApplicationID()
        {
            return 40233;
        }

        private static async Task<string> GetEncodedJwtToken()
        {
            // TODO: Need to introduce caching here so that each
            //       webhook doesn't hit KeyVault.
            var generator = new GitHubJwtFactory(
                GetPrivateKeySource(),
                new GitHubJwtFactoryOptions()
                {
                    AppIntegrationId = GetApplicationID(),
                    ExpirationSeconds = 600
                }
            );

            var token = generator.CreateEncodedJwtToken();

            return token;
        }

        private static async Task<GitHubClient> GetApplicationClientAsync()
        {
            var token = await GetEncodedJwtToken();

            var appClient = new GitHubClient(new ProductHeaderValue(ApplicationName))
            {
                Credentials = new Credentials(token, AuthenticationType.Bearer)
            };

            return appClient;
        }

        private static async Task<AccessToken> GetInstallationTokenAsync(long installationId)
        {
            var appClient = await GetApplicationClientAsync();

            // TODO: Need to introduce token caching here since we don't want
            //       to get rate limited hitting this API.
            var installationToken = await appClient.GitHubApps.CreateInstallationToken(installationId);
            return installationToken;
        }

        private static async Task<GitHubClient> GetInstallationClientAsync(long installationId)
        {
            var installationToken = await GetInstallationTokenAsync(installationId);
            var installationClient = new GitHubClient(new ProductHeaderValue($"{ApplicationName}-{installationId}"))
            {
                Credentials = new Credentials(installationToken.Token)
            };

            return installationClient;
        }

        private static async Task<TEvent> DeserializePayloadAsync<TEvent>(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                var payloadString = await reader.ReadToEndAsync();
                var serializer = new SimpleJsonSerializer();
                var payload = serializer.Deserialize<TEvent>(payloadString);
                return payload;
            }
        }

        [FunctionName("webhook")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            if (req.Headers.TryGetValue(GitHubEventHeader, out StringValues checkSuiteEvent) && checkSuiteEvent == "check_suite")
            {
                var payload = await DeserializePayloadAsync<CheckSuiteEventPayload>(req.Body);
                var installationId = payload.Installation.Id;
                var repositoryId = payload.Repository.Id;
                var sha = payload.CheckSuite.HeadSha;

                var installationClient = await GetInstallationClientAsync(installationId);

                var runsResponse = await installationClient.Check.Run.GetAllForReference(repositoryId, sha);
                var runs = runsResponse.CheckRuns;

                var checkEnforcerCheckRun = runs.SingleOrDefault(cr => cr.Name == ApplicationName);
                
                if (checkEnforcerCheckRun == null)
                {
                    await CreateCheckRunAsync(payload, repositoryId, installationClient);
                }
                else
                {
                    await UpdateCheckRunAsync(repositoryId, installationClient, runs, checkEnforcerCheckRun);
                }

                return new OkResult();
            }
            if (req.Headers.TryGetValue(GitHubEventHeader, out StringValues checkRunEvent) && checkRunEvent == "check_run")
            {
                var payload = await DeserializePayloadAsync<CheckRunEventPayload>(req.Body);
                return new OkResult();
            }
            else
            {
                // Anything else is an error.
                return new BadRequestResult();
            }
        }

        private static async Task UpdateCheckRunAsync(long repositoryId, GitHubClient installationClient, IEnumerable<CheckRun> runs, CheckRun run)
        {
            var oustandingChecksCount = (from cr in runs
                                         where cr.Name != ApplicationName
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

        private static async Task CreateCheckRunAsync(CheckSuiteEventPayload payload, long repositoryId, GitHubClient installationClient)
        {
            var checkRun = await installationClient.Check.Run.Create(
                repositoryId,
                new NewCheckRun(ApplicationName, payload.CheckSuite.HeadSha)
            );

            checkRun = await installationClient.Check.Run.Update(repositoryId, checkRun.Id, new CheckRunUpdate()
            {
                Status = new StringEnum<CheckStatus>(CheckStatus.InProgress)
            });
        }
    }
}
