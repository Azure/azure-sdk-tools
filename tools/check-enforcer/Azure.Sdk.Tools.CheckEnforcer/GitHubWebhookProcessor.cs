using Azure.Core;
using Azure.Identity;
using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Handlers;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Host;
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
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class GitHubWebhookProcessor
    {
        public GitHubWebhookProcessor(IGlobalConfigurationProvider globalConfigurationProvider, IGitHubClientProvider gitHubClientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider)
        {
            this.globalConfigurationProvider = globalConfigurationProvider;
            this.gitHubClientProvider = gitHubClientProvider;
            this.repositoryConfigurationProvider = repositoryConfigurationProvider;
        }
        
        public IGlobalConfigurationProvider globalConfigurationProvider;
        public IGitHubClientProvider gitHubClientProvider;
        private IRepositoryConfigurationProvider repositoryConfigurationProvider;
        private const string GitHubEventHeader = "X-GitHub-Event";
        private const string GitHubSignatureHeader = "X-Hub-Signature";

        private DateTimeOffset gitHubAppWebhookSecretExpiry = DateTimeOffset.MinValue;
        private string gitHubAppWebhookSecret;
        private SecretClient secretClient;

        private async Task<string> GetGitHubAppWebhookSecretAsync(CancellationToken cancellationToken)
        {
            if (gitHubAppWebhookSecretExpiry < DateTimeOffset.UtcNow)
            {
                var gitHubAppWebhookSecretName = globalConfigurationProvider.GetGitHubAppWebhookSecretName();

                var client = GetSecretClient();
                var response = await client.GetSecretAsync(gitHubAppWebhookSecretName, cancellationToken: cancellationToken);
                var secret = response.Value;

                gitHubAppWebhookSecretExpiry = DateTimeOffset.UtcNow.AddSeconds(30);
                gitHubAppWebhookSecret = secret.Value;
            }

            return gitHubAppWebhookSecret;
        }

        private SecretClient GetSecretClient()
        {
            var keyVaultUri = globalConfigurationProvider.GetKeyVaultUri();
            var credential = new DefaultAzureCredential();

            if (secretClient == null)
            {
                secretClient = new SecretClient(new Uri(keyVaultUri), credential);
            }

            return secretClient;
        }

        private async Task<string> ReadAndVerifyBodyAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            using (var reader = new StreamReader(request.Body))
            {
                var json = await reader.ReadToEndAsync();
                var jsonBytes = Encoding.UTF8.GetBytes(json);

                if (request.Headers.TryGetValue(GitHubSignatureHeader, out StringValues signature))
                {
                    var secret = await GetGitHubAppWebhookSecretAsync(cancellationToken);

                    var isValid = GitHubWebhookSignatureValidator.IsValid(jsonBytes, signature, secret);
                    if (isValid)
                    {
                        return json;
                    }
                    else
                    {
                        throw new CheckEnforcerSecurityException("Webhook signature validation failed.");
                    }
                }
                else
                {
                    throw new CheckEnforcerSecurityException("Webhook missing event signature.");
                }
            }
        }

        public async Task ProcessWebhookAsync(HttpRequest request, ILogger logger, CancellationToken cancellationToken)
        {
            var json = await ReadAndVerifyBodyAsync(request, cancellationToken);

            if (request.Headers.TryGetValue(GitHubEventHeader, out StringValues eventName))
            {
                if (eventName == "check_run")
                {
                    var handler = new CheckRunHandler(globalConfigurationProvider, gitHubClientProvider, repositoryConfigurationProvider, logger);
                    await handler.HandleAsync(json, cancellationToken);
                }
                else if (eventName == "check_suite")
                {
                    var handler = new CheckSuiteHandler(globalConfigurationProvider, gitHubClientProvider, repositoryConfigurationProvider, logger);
                    await handler.HandleAsync(json, cancellationToken);
                }
                else if (eventName == "issue_comment")
                {
                    var handler = new IssueCommentHandler(globalConfigurationProvider, gitHubClientProvider, repositoryConfigurationProvider, logger);
                    await handler.HandleAsync(json, cancellationToken);
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
