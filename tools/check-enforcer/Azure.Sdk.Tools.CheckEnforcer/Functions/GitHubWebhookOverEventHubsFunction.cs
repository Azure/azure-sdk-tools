using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Azure.Sdk.Tools.CheckEnforcer.Functions
{
    public class GitHubWebhookOverEventHubsFunction
    {
        public GitHubWebhookOverEventHubsFunction(GitHubWebhookProcessor processor, SecretClient secretClient)
        {
            this.processor = processor;
            this.secretClient = secretClient;
        }

        private GitHubWebhookProcessor processor;
        private SecretClient secretClient;

        [FunctionName("webhook-eventhubs")]
        public async Task Run([EventHubTrigger("github-webhooks", Connection = "CheckEnforcerEventHubConnectionString")] EventData eventData, ILogger log, CancellationToken cancellationToken)
        {
            var message = GetMessage(eventData);
            var eventName = GetEventName(message);
            var eventSignature = GetEventSignature(message);

            var encodedContent = message.RootElement.GetProperty("content").ToString();
            var contentBytes = Convert.FromBase64String(encodedContent);
            var json = ReadAndVerifyContent(contentBytes, eventSignature);

            await processor.ProcessWebhookAsync(eventName, json, log, cancellationToken);
        }

        private static string gitHubAppWebhookSecret;
        private static object gitHubAppWebhookSecretLock = new object();

        private const string GitHubWebhookSecretName = "github-app-webhook-secret";

        private string GetGitHubAppWebhookSecret()
        {
            if (gitHubAppWebhookSecret == null)
            {
                lock (gitHubAppWebhookSecretLock)
                {
                    if (gitHubAppWebhookSecret == null)
                    {
                        KeyVaultSecret secret = secretClient.GetSecret(GitHubWebhookSecretName);
                        gitHubAppWebhookSecret = secret.Value;
                    }
                }
            }

            return gitHubAppWebhookSecret;
        }

        private string ReadAndVerifyContent(byte[] contentBytes, string signature)
        {
            var secret = GetGitHubAppWebhookSecret();
            var isValid = GitHubWebhookSignatureValidator.IsValid(contentBytes, signature, secret);
                 
            if (!isValid)
            {
                throw new CheckEnforcerSecurityException("Webhook signature validation failed.");
            }

            var content = Encoding.UTF8.GetString(contentBytes);
            return content;
        }

        private string GetEventName(JsonDocument message)
        {
            return message
                .RootElement
                .GetProperty("headers")
                .GetProperty("X-GitHub-Event")
                .EnumerateArray()
                .Single()
                .ToString();
        }

        private string GetEventSignature(JsonDocument message)
        {
            return message
                .RootElement
                .GetProperty("headers")
                .GetProperty(GitHubWebhookSignatureValidator.GitHubWebhookSignatureHeader)
                .EnumerateArray()
                .Single()
                .ToString();
        }

        private JsonDocument GetMessage(EventData eventData)
        {
            string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
            var message = JsonDocument.Parse(messageBody);
            return message;
        }
    }
}