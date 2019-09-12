using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Octokit;
using Octokit.Internal;
using System.Collections;
using System.Collections.Generic;
using Azure.Security.KeyVault.Keys;
using Azure.Identity;
using System.Threading;
using Azure.Security.KeyVault.Keys.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Cryptography;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public static class GitHubWebhookFunction
    {
        private const string ApplicationName = "check-enforcer";
        private const string GitHubEventHeader = "X-GitHub-Event";

        private static int GetApplicationID()
        {
            return 40233;
        }

        private static async Task<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            try
            {
                var credential = new DefaultAzureCredential();

                // TODO: Consider replacing these explicit environment variables with
                //       a call to Azure App config service which is identifier based
                //       on some kind of environmental convention.
                var keyVaultUriEnvironmentVariable = Environment.GetEnvironmentVariable("KEYVAULT_URI");
                var keyVaultUri = new Uri(keyVaultUriEnvironmentVariable);
                var keyClient = new KeyClient(keyVaultUri, credential);

                var keyVaultGitHubKeyName = Environment.GetEnvironmentVariable("KEYVAULT_GITHUBAPP_KEY_NAME");

                var keyResponse = await keyClient.GetKeyAsync(
                    keyVaultGitHubKeyName,
                    cancellationToken: cancellationToken
                    );

                var key = keyResponse.Value;

                var cryptographyClient = new CryptographyClient(key.Id, credential);

                var jwtHeader = new JwtHeader();
                jwtHeader["alg"] = "RS256";

                var jwtPayload = new JwtPayload(
                    issuer: GetApplicationID().ToString(),
                    audience: null,
                    claims: null,
                    notBefore: DateTime.UtcNow,
                    expires: DateTime.UtcNow.AddMinutes(10),
                    issuedAt: DateTime.UtcNow
                    );

                var jwtToken = new JwtSecurityToken(jwtHeader, jwtPayload);

                var headerAndPayloadString = $"{jwtToken.EncodedHeader}.{jwtToken.EncodedPayload}";

                var headerAndPayloadBytes = Encoding.UTF8.GetBytes(headerAndPayloadString);

                var sha256 = new SHA256CryptoServiceProvider();
                var digest = sha256.ComputeHash(headerAndPayloadBytes);


                var signResult = await cryptographyClient.SignAsync(
                    SignatureAlgorithm.RS256,
                    digest,
                    cancellationToken
                    );

                // TODO: We need to compute the SHA256 hash here to pass in as the digest, otherwise KeyVault
                //       will reject it!

                var encodedSignature = Base64UrlEncoder.Encode(signResult.Signature);

                var encodedToken = $"{headerAndPayloadString}.{encodedSignature}";

                return encodedToken;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static async Task<GitHubClient> GetApplicationClientAsync(CancellationToken cancellationToken)
        {
            //var token = await GetEncodedJwtToken();

            var token = await GetTokenAsync(cancellationToken);

            var appClient = new GitHubClient(new ProductHeaderValue(ApplicationName))
            {
                Credentials = new Credentials(token, AuthenticationType.Bearer)
            };

            return appClient;
        }

        private static async Task<AccessToken> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken)
        {
            var appClient = await GetApplicationClientAsync(cancellationToken);

            // TODO: Need to introduce token caching here since we don't want
            //       to get rate limited hitting this API.
            var installationToken = await appClient.GitHubApps.CreateInstallationToken(installationId);
            return installationToken;
        }

        private static async Task<GitHubClient> GetInstallationClientAsync(long installationId, CancellationToken cancellationToken)
        {
            var installationToken = await GetInstallationTokenAsync(installationId, cancellationToken);
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
            ILogger log, CancellationToken cancellationToken)
        {
            if (req.Headers.TryGetValue(GitHubEventHeader, out StringValues checkSuiteEvent) && checkSuiteEvent == "check_suite")
            {
                var payload = await DeserializePayloadAsync<CheckSuiteEventPayload>(req.Body);
                var installationId = payload.Installation.Id;
                var repositoryId = payload.Repository.Id;
                var sha = payload.CheckSuite.HeadSha;

                var installationClient = await GetInstallationClientAsync(installationId, cancellationToken);

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
