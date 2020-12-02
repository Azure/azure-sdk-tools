using Azure.Data.AppConfiguration;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Azure.Sdk.Tools.WebhookRouter.Integrations.GitHub;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    public class Router : IRouter
    {
        public Router(IMemoryCache cache, ILogger<IRouter> logger, ConfigurationClient configurationClient, SecretClient secretClient)
        {
            this.cache = cache;
            this.logger = logger;
            this.configurationClient = configurationClient;
            this.secretClient = secretClient;
        }

        private IMemoryCache cache;
        private ILogger logger;
        private ConfigurationClient configurationClient;
        private SecretClient secretClient;

        private async Task<string> GetSecretAsync(string secretName)
        {
            var secretCacheKey = $"{secretName}_secretCacheKey";

            logger.LogInformation("Fetching secret with cache key: {secretCacheKey}", secretCacheKey);

            var cachedSecret = await cache.GetOrCreateAsync<string>(secretCacheKey, async (entry) =>
            {
                logger.LogInformation("Cache empty, fetching secret from KeyVault for cache key: {secretCacheKey}", secretCacheKey);

                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);

                KeyVaultSecret response = await secretClient.GetSecretAsync(secretName);
                var secret = response.Value;
                return secret;
            });

            return cachedSecret;
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

            logger.LogInformation("Fetching settings for route {route} with filter {filter}", route, settingSelectorKeyFilter);

            var settingSelector = new SettingSelector()
            {
                KeyFilter = settingSelectorKeyFilter
            };

            var settingsPages = configurationClient.GetConfigurationSettingsAsync(settingSelector);

            var settings = new Dictionary<string, string>();
            await foreach (var setting in settingsPages)
            {
                var relativeSettingKey = setting.Key.Substring(settingSelectorKeyPrefix.Length);
                settings.Add(relativeSettingKey, setting.Value);
            }

            if (!settings.ContainsKey("payload-type") && !settings.ContainsKey("eventhubs-namespace") && !settings.ContainsKey("eventhub-name"))
            {
                throw new RouterConfigurationException($"Critical settings for route {route} not present.");
            }

            return settings;
        }

        private async Task<Rule> GetRuleAsync(Guid route)
        {
            logger.LogInformation("Fetching routing rule for route: {route}", route);

            var settings = await GetSettingsAsync(route);

            logger.LogInformation("Route {route} had {dictionaryCount} in settings dictionary.", route, settings.Count);

            var payloadType = (PayloadType)Enum.Parse(typeof(PayloadType), settings["payload-type"], true);
            var eventHubsNamespace = settings["eventhubs-namespace"];
            var eventHubName = settings["eventhub-name"];

            logger.LogInformation(
                "Route {route} points to namespace {namespace} with hub name {hubName} with payload type {payloadType}.",
                route,
                eventHubsNamespace,
                eventHubName,
                payloadType
                );

            Rule rule = payloadType switch
            {
                PayloadType.GitHub => new GitHubRule(
                    route,
                    eventHubsNamespace,
                    eventHubName,
                    settings["github-webhook-secret"]),
                PayloadType.AzureDevOps => new AzureDevOpsRule(
                    route,
                    eventHubsNamespace,
                    eventHubName,
                    settings["azure-devops-webhook-credential-hash"],
                    settings["azure-devops-webhook-credential-salt"]),
                _ => new GenericRule(
                    route,
                    eventHubsNamespace,
                    eventHubName)
            };

            return rule;
        }

        private async Task<byte[]> ReadAndValidateContentFromGitHubAsync(GitHubRule rule, HttpRequest request)
        {
            var payloadContent = await ReadAndValidateContentFromGenericAsync(rule, request);

            var secret = await GetSecretAsync(rule.WebhookSecret);
            var signature = request.Headers[GitHubWebhookSignatureValidator.GitHubWebhookSignatureHeader];
            bool isValid = GitHubWebhookSignatureValidator.IsValid(payloadContent, signature, secret);

            if (!isValid)
            {
                throw new RouterAuthorizationException("Signature validation failed.");
            }

            return payloadContent;
        }

        private SHA256CryptoServiceProvider sha256 = new SHA256CryptoServiceProvider();

        private async Task<byte[]> ReadAndValidateContentFromAzureDevOpsAsync(AzureDevOpsRule rule, HttpRequest request)
        {
            var payloadContent = await ReadAndValidateContentFromGenericAsync(rule, request);

            var credentialHash = await GetSecretAsync(rule.CredentialHash);
            var credentialSalt = await GetSecretAsync(rule.CredentialSalt);

            var authorizationHeader = request.Headers["Authorization"].ToString();
            var base64EncodedCredentials = authorizationHeader.Replace("Basic ", "");

            var base64EncodedCredentialsWithSalt = $"{base64EncodedCredentials}{credentialSalt}";
            var base64EncodedCredentialsWithSaltBytes = Encoding.UTF8.GetBytes(base64EncodedCredentialsWithSalt);
            var generatedCredentialHashBytes = sha256.ComputeHash(base64EncodedCredentialsWithSaltBytes);
            var generatedCredentialHash = Convert.ToBase64String(generatedCredentialHashBytes);

            if (credentialHash != generatedCredentialHash)
            {
                throw new RouterAuthorizationException("Credential validation failed.");
            }

            return payloadContent;
        }

        private async Task<byte[]> ReadContentFromRequest(HttpRequest request)
        {
            using var stream = new MemoryStream();
            await request.Body.CopyToAsync(stream);
            var payloadContent = stream.ToArray();

            logger.LogInformation("Payload length was {length} bytes.", payloadContent.Length);

            return payloadContent;
        }

        private async Task<byte[]> ReadAndValidateContentFromGenericAsync(Rule rule, HttpRequest request)
        {
            // At this point generic rules don't do anything
            // other than read the content from the request
            // and return it. However, for the sake of clarity
            // I've broken the logic to do this into a seperate
            // method so it can be used in a seperate exception
            // handling scenario when a rule does not exist.
            return await ReadContentFromRequest(request);
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

            logger.LogInformation("Fetching cached EventHubProducerClient for cache key: {cacheKey}", eventHubProducerClientCacheKey);

            var cachedProdocer = cache.GetOrCreate<EventHubProducerClient>(eventHubProducerClientCacheKey, (entry) =>
            {
                logger.LogInformation("Cache empty, populating cache key: {cacheKey}", eventHubProducerClientCacheKey);

                var fullyQualifiedEventHubsNamespace = $"{eventHubsNamespace}.servicebus.windows.net";
                var credential = new DefaultAzureCredential();
                var producer = new EventHubProducerClient(fullyQualifiedEventHubsNamespace, eventHubName, credential);
                return producer;
            });

            return cachedProdocer;
        }

        public async Task RouteAsync(Guid route, HttpRequest request)
        {
            try
            {
                logger.LogInformation("Routing request for route: {route}", route);

                var rule = await GetRuleAsync(route);
                var payload = await CreateAndValidatePayloadAsync(rule, request);

                var payloadJson = JsonSerializer.Serialize(payload);

                logger.LogInformation("Payload Content: {payloadContent}", payload.Content);

                var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

                var @event = new EventData(payloadBytes);

                logger.LogInformation(
                    "Sending event from route {route} to namespace {namespace} with hub name {hubName}",
                    route,
                    rule.EventHubsNamespace,
                    rule.EventHubName
                    );

                var producer = GetEventHubProducerClient(rule.EventHubsNamespace, rule.EventHubName);
                var batch = await producer.CreateBatchAsync();
                batch.TryAdd(@event);
                await producer.SendAsync(batch);

                logger.LogInformation(
                    "Sent event from route {route} to namespace {namespace} with hub name {hubName}",
                    route,
                    rule.EventHubsNamespace,
                    rule.EventHubName
                    );
            }
            catch (RouterConfigurationException ex)
            {
                var payloadBytes = await ReadContentFromRequest(request);
                var payload = Encoding.UTF8.GetString(payloadBytes);
                logger.LogError(
                    ex,
                    "Router configuration error, payload was: {payload}",
                    payload
                    );

                throw ex;
            }
        }
    }
}
