using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public GitHubWebhookOverEventHubsFunction(GitHubWebhookProcessor processor, SecretClient secretClient, ILogger<GitHubWebhookOverEventHubsFunction> logger)
        {
            this.processor = processor;
            this.secretClient = secretClient;
            this.logger = logger;
        }

        private GitHubWebhookProcessor processor;
        private SecretClient secretClient;
        private ILogger<GitHubWebhookOverEventHubsFunction> logger;

        [FunctionName("webhook-eventhubs")]
        public async Task Run([EventHubTrigger("github-webhooks", Connection = "CheckEnforcerEventHubConnectionString", ConsumerGroup = "localdebugging")] EventData[] eventData, CancellationToken cancellationToken)
        {
            var events = new List<GitHubWebhookEvent>();

            foreach (var singleEvent in eventData)
            {
                var message = GetMessage(singleEvent);
                var eventName = GetEventName(message);
                var eventSignature = GetEventSignature(message);
                var encodedContent = message.RootElement.GetProperty("content").ToString();
                var contentBytes = Convert.FromBase64String(encodedContent);

                try
                {
                    var json = ReadAndVerifyContent(contentBytes, eventSignature);
                    events.Add(new GitHubWebhookEvent(eventName, json));
                }
                catch (CheckEnforcerSecurityException ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to process message in batch due to failed signature. Payload was: {payload}",
                        encodedContent
                        );
                }
            }

            await processor.ProcessWebhooksAsync(events, cancellationToken);
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