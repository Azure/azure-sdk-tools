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
        private const string ApplicationName = "check-enforcer";
        private const string GitHubEventHeader = "X-GitHub-Event";
        private const uint ApplicationTokenLifetimeInMinutes = 10;

        private int GetApplicationID()
        {
            return 40233;
        }

        private Tuple<DateTimeOffset, string> cachedApplicationToken;

        private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            if (cachedApplicationToken == null || cachedApplicationToken.Item1 < DateTimeOffset.UtcNow)
            {
                // NOTE: There is potential for a cache stampeed issue here, but it may
                //       not be enough of an issue to worry about it. Will need to evaluate
                //       once we get some realistic load numbers. If necessary we can switch
                //       to a sync programming model and use locking.
                //
                //       Cache stampeed will be visible in the KeyVault diagostics because
                //       we'll see a spike in get and sign requests. Stampeed duration will
                //       be limited in duration.
                var headerAndPayloadString = GenerateJwtTokenHeaderAndPayload();
                var digest = ComputeHeaderAndPayloadDigest(headerAndPayloadString);
                var encodedSignature = await SignHeaderAndPayloadDigestWithGitHubApplicationKey(digest, cancellationToken);
                var token = AppendSignatureToHeaderAndPayload(headerAndPayloadString, encodedSignature);

                // Let's get a token a full minute before it times out.
                cachedApplicationToken = new Tuple<DateTimeOffset, string>(
                    DateTimeOffset.UtcNow.AddMinutes(ApplicationTokenLifetimeInMinutes - 1),
                    token
                    );
            }

            return cachedApplicationToken.Item2;
        }

        private string AppendSignatureToHeaderAndPayload(string headerAndPayloadString, string encodedSignature)
        {
            return $"{headerAndPayloadString}.{encodedSignature}";
        }

        private async Task<string> SignHeaderAndPayloadDigestWithGitHubApplicationKey(byte[] digest, CancellationToken cancellationToken)
        {
            var cryptographyClient = await GetCryptographyClient(cancellationToken);
            var signResult = await cryptographyClient.SignAsync(
                SignatureAlgorithm.RS256,
                digest,
                cancellationToken
                );

            var encodedSignature = Base64UrlEncoder.Encode(signResult.Signature);
            return encodedSignature;
        }

        private byte[] ComputeHeaderAndPayloadDigest(string headerAndPayloadString)
        {
            var headerAndPayloadBytes = Encoding.UTF8.GetBytes(headerAndPayloadString);

            var sha256 = new SHA256CryptoServiceProvider();
            var digest = sha256.ComputeHash(headerAndPayloadBytes);
            return digest;
        }

        private string GenerateJwtTokenHeaderAndPayload()
        {
            var jwtHeader = new JwtHeader();
            jwtHeader["alg"] = "RS256";

            var jwtPayload = new JwtPayload(
                issuer: GetApplicationID().ToString(),
                audience: null,
                claims: null,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(ApplicationTokenLifetimeInMinutes),
                issuedAt: DateTime.UtcNow
                );

            var jwtToken = new JwtSecurityToken(jwtHeader, jwtPayload);
            var headerAndPayloadString = $"{jwtToken.EncodedHeader}.{jwtToken.EncodedPayload}";
            return headerAndPayloadString;
        }

        private async Task<CryptographyClient> GetCryptographyClient(CancellationToken cancellationToken)
        {
            // Using DefaultAzureCredential to support local development. If developing
            // locally you'll need to register an AAD application and set the following
            // variables:
            //
            //      AZURE_TENANT_ID (the ID of the AAD tenant)
            //      AZURE_CLIENT_ID (the iD of the AAD application you registered)
            //      AZURE_CLIENT_SECRET (the secret for the AAD application you registered)
            //
            // You can get these values when you configure the application. Set them in
            // the Debug section of the project properties. Once this is done you will need
            // to create a KeyVault instance and then register a GitHub application and upload
            // the private key into the vault. The AAD application that you just created needs
            // to have Get and Sign rights - so set an access policy up which grants the app
            // those rights.
            //
            var credential = new DefaultAzureCredential();

            var keyClient = GetKeyClient(credential);
            var key = await GetKey(keyClient, cancellationToken);

            var cryptographyClient = new CryptographyClient(key.Id, credential);
            return cryptographyClient;
        }

        private  async Task<Key> GetKey(KeyClient keyClient, CancellationToken cancellationToken)
        {
            var keyVaultGitHubKeyName = Environment.GetEnvironmentVariable("KEYVAULT_GITHUBAPP_KEY_NAME");

            var keyResponse = await keyClient.GetKeyAsync(
                keyVaultGitHubKeyName,
                cancellationToken: cancellationToken
                );

            var key = keyResponse.Value;
            return key;
        }

        private KeyClient GetKeyClient(TokenCredential credential)
        {

            // We need a variable that tells Check Enforcer which KeyVault to talk to,
            // this is currently done via an environment variable.
            //
            //      KEYVAULT_URI
            //
            var keyVaultUriEnvironmentVariable = Environment.GetEnvironmentVariable("KEYVAULT_URI");

            var keyVaultUri = new Uri(keyVaultUriEnvironmentVariable);
            var keyClient = new KeyClient(keyVaultUri, credential);
            return keyClient;
        }

        private async Task<GitHubClient> GetApplicationClientAsync(CancellationToken cancellationToken)
        {
            var token = await GetTokenAsync(cancellationToken);

            var appClient = new GitHubClient(new ProductHeaderValue(ApplicationName))
            {
                Credentials = new Credentials(token, AuthenticationType.Bearer)
            };

            return appClient;
        }

        private Dictionary<long, Octokit.AccessToken> cachedInstallationTokens = new Dictionary<long, Octokit.AccessToken>();

        private async Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken)
        {
            if (!cachedInstallationTokens.ContainsKey(installationId) || cachedInstallationTokens[installationId].ExpiresAt < DateTimeOffset.UtcNow)
            {
                // NOTE: There is a possible cache stampeed here as well. We'll see in time
                //       if we need to do anything about it. If there is a problem it will
                //       manifest as exceptions being thrown out of this method.
                var appClient = await GetApplicationClientAsync(cancellationToken);
                var accessToken = await appClient.GitHubApps.CreateInstallationToken(installationId);
                cachedInstallationTokens[installationId] = accessToken;
            }

            var installationToken = cachedInstallationTokens[installationId];
            return installationToken.Token;
        }

        private async Task<GitHubClient> GetInstallationClientAsync(long installationId, CancellationToken cancellationToken)
        {
            var installationToken = await GetInstallationTokenAsync(installationId, cancellationToken);
            var installationClient = new GitHubClient(new ProductHeaderValue($"{ApplicationName}-{installationId}"))
            {
                Credentials = new Credentials(installationToken)
            };

            return installationClient;
        }

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

        private async Task CreateCheckRunAsync(CheckSuiteEventPayload payload, long repositoryId, GitHubClient installationClient)
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

        public async Task ProcessWebhookAsync(HttpRequest request, ILogger log, CancellationToken cancellationToken)
        {
            if (request.Headers.TryGetValue(GitHubEventHeader, out StringValues eventName))
            {
                if (eventName == "check_suite")
                {
                    var payload = await DeserializePayloadAsync<CheckSuiteEventPayload>(request.Body);
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
                }
                else if (eventName == "check_run")
                {
                    var payload = await DeserializePayloadAsync<CheckRunEventPayload>(request.Body);
                }
                else
                {
                    throw new GitHubWebhookHandlerNotRegisteredException(eventName);
                }
            }
            else
            {
                throw new GitHubWebhookProcessorException($"Could not find header '{GitHubEventHeader}'.");
            }
        }
    }
}
