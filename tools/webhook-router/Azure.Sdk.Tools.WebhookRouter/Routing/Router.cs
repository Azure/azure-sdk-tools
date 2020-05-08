using Azure.Data.AppConfiguration;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Azure.Sdk.Tools.WebhookRouter.Integrations.GitHub;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    public class Router : IRouter
    {
        public Router(IMemoryCache cache)
        {
            this.cache = cache;
        }

        private IMemoryCache cache;

        private Uri GetSecretClientUri()
        {
            var websiteResourceGroupEnvironmentVariable = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP");
            var uri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.vault.azure.net/");
            return uri;
        }

        private Uri GetConfigurationUri()
        {
            var websiteResourceGroupEnvironmentVariable = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP");
            var uri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.azconfig.io/");
            return uri;
        }

        private SecretClient GetSecretClient()
        {
            var uri = GetSecretClientUri();
            var credential = new DefaultAzureCredential();
            var client = new SecretClient(uri, credential);
            return client;
        }

        private async Task<string> GetSecretAsync(string secretName)
        {
            var secretCacheKey = $"{secretName}_secretCacheKey";

            var cachedSecret = await cache.GetOrCreateAsync<string>(secretCacheKey, async (entry) =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);

                var client = GetSecretClient();
                var response = await client.GetSecretAsync(secretName);
                var secret = response.Value.Value; // Urgh!
                return secret;
            });

            return cachedSecret;
        }

        private ConfigurationClient GetConfigurationClient()
        {
            var uri = GetConfigurationUri();
            var credential = new DefaultAzureCredential();
            var client = new ConfigurationClient(uri, credential);
            return client;
        }

        // This key filter template is used to select all the configuration values
        // for a particular routing rule. We do this to fetch all the configuration
        // values at once rather than making a call to the app config service
        // multiple times.
        private const string SettingSelectorKeyPrefixTemplate = "webhookrouter/rules/{0}/";
        
        private async Task<Dictionary<string, string>> GetSettingsAsync(Guid route)
        {
            var routeSettingsCacheKey = $"{route}_routeSettingsCacheKey";
            var cachedSettings = await cache.GetOrCreateAsync<Dictionary<string, string>>(routeSettingsCacheKey, async (entry) =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);

                return await GetSettingsFromAppConfigurationService(route);
            });

            return cachedSettings;
        }

        private async Task<Dictionary<string, string>> GetSettingsFromAppConfigurationService(Guid route)
        {
            var settingSelectorKeyPrefix = string.Format(SettingSelectorKeyPrefixTemplate, route);
            var settingSelectorKeyFilter = $"{settingSelectorKeyPrefix}*";

            var settingSelector = new SettingSelector()
            {
                KeyFilter = settingSelectorKeyFilter
            };

            var client = GetConfigurationClient();
            var settingsPages = client.GetConfigurationSettingsAsync(settingSelector);

            var settings = new Dictionary<string, string>();
            await foreach (var setting in settingsPages)
            {
                var relativeSettingKey = setting.Key.Substring(settingSelectorKeyPrefix.Length);
                settings.Add(relativeSettingKey, setting.Value);
            }

            if (!settings.ContainsKey("payload-type") && !settings.ContainsKey("eventhubs-namespace") && !settings.ContainsKey("eventhub-name"))
            {
                throw new RouterException($"Critical settings for route {route} not present.");
            }

            return settings;
        }

        private async Task<Rule> GetRuleAsync(Guid route)
        {
            var settings = await GetSettingsAsync(route);

            var payloadType = (PayloadType)Enum.Parse(typeof(PayloadType), settings["payload-type"], true);
            var eventHubsNamespace = settings["eventhubs-namespace"];
            var eventHubName = settings["eventhub-name"];

            Rule rule = payloadType switch
            {
                PayloadType.GitHub => new GitHubRule(route, eventHubsNamespace, eventHubName, settings["github-webhook-secret"]),
                PayloadType.AzureDevOps => new AzureDevOpsRule(route, eventHubsNamespace, eventHubName),
                _ => new GenericRule(route, eventHubsNamespace, eventHubName)
            };

            return rule;
        }

        private async Task<byte[]> ReadAndValidateContentFromGitHubAsync(GitHubRule rule, HttpRequest request)
        {
            var payloadContent = await ReadAndValidateContentFromGenericAsync(rule, request);

            var secret = await GetSecretAsync(rule.WebhookSecret);
            var signature = request.Headers[GitHubWebhookSignatureValidator.GitHubWebhookSignatureHeader];
            bool isValid = GitHubWebhookSignatureValidator.IsValid(payloadContent, signature, secret);

            return payloadContent;
        }

        private async Task<byte[]> ReadAndValidateContentFromAzureDevOpsAsync(AzureDevOpsRule rule, HttpRequest request)
        {
            var payloadContent = await ReadAndValidateContentFromGenericAsync(rule, request);
            return payloadContent;
        }

        private async Task<byte[]> ReadAndValidateContentFromGenericAsync(Rule rule, HttpRequest request)
        {
            using var stream = new MemoryStream();
            await request.Body.CopyToAsync(stream);
            var payloadContent = stream.ToArray();
            return payloadContent;
        }

        private async Task<Payload> CreateAndValidatePayloadAsync(Rule rule, HttpRequest request)
        {
            var payloadContent = rule switch
            {
                GitHubRule gitHubRule => await ReadAndValidateContentFromGitHubAsync(gitHubRule, request),
                AzureDevOpsRule azureDevopsRule => await ReadAndValidateContentFromAzureDevOpsAsync(azureDevopsRule, request),
                _ => await ReadAndValidateContentFromGenericAsync(rule, request)
            };

            var payload = new Payload(request.Headers, payloadContent);
            return payload;
        }

        private EventHubProducerClient GetEventHubProducerClient(string eventHubsNamespace, string eventHubName)
        {
            var eventHubProducerClientCacheKey = $"{eventHubsNamespace}/{eventHubName}_eventHubProducerClientCacheKey";

            var cachedProdocer = cache.GetOrCreate<EventHubProducerClient>(eventHubProducerClientCacheKey, (entry) =>
            {
                var fullyQualifiedEventHubsNamespace = $"{eventHubsNamespace}.servicebus.windows.net";
                var credential = new DefaultAzureCredential();
                var producer = new EventHubProducerClient(fullyQualifiedEventHubsNamespace, eventHubName, credential);
                return producer;
            });

            return cachedProdocer;
        }

        public async Task RouteAsync(Guid route, HttpRequest request)
        {
            var rule = await GetRuleAsync(route);
            var payload = await CreateAndValidatePayloadAsync(rule, request);

            var payloadJson = JsonSerializer.Serialize(payload);
            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

            var @event = new EventData(payloadBytes);

            var producer = GetEventHubProducerClient(rule.EventHubsNamespace, rule.EventHubName);
            var batch = await producer.CreateBatchAsync();
            batch.TryAdd(@event);
            await producer.SendAsync(batch);
        }
    }
}
