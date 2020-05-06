using Azure.Data.AppConfiguration;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    public class Router : IRouter
    {

        private Uri GetConfigurationUri()
        {
            var websiteResourceGroupEnvironmentVariable = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP");
            var uri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.azconfig.io");
            return uri;
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

        public async Task<Rule> GetRuleAsync(Guid route)
        {
            var settings = await GetSettingsAsync(route);
            var rule = new Rule(route, settings);
            return rule;
        }

        public async Task RouteAsync(Rule rule, Payload payload)
        {
            var payloadString = JsonSerializer.Serialize(payload, new JsonSerializerOptions()
            {
                WriteIndented = true
            });

            var payloadBytes = Encoding.UTF8.GetBytes(payloadString);
            var @event = new EventData(payloadBytes);

            var fullyQualifiedEventHubsNamespace = $"{rule.EventHubsNamespace}.servicebus.windows.net";
            var credential = new DefaultAzureCredential();
            var producer = new EventHubProducerClient(fullyQualifiedEventHubsNamespace, rule.EventHubName, credential);
            var batch = await producer.CreateBatchAsync();
            batch.TryAdd(@event);
            await producer.SendAsync(batch);
        }
    }
}
