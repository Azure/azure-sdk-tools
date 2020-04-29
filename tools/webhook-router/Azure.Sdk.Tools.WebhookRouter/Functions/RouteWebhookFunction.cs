using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Identity;
using Azure.Data.AppConfiguration;

namespace Azure.Sdk.Tools.WebhookRouter.Functions
{
    public class RouteWebhookFunction
    {
        [FunctionName("route")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "route/{route}")] HttpRequest req,
            ILogger log,
            string route)
        {
            var credential = new DefaultAzureCredential();
            var client = new ConfigurationClient(new Uri("https://webhookrouterstaging.azconfig.io/"), credential);

            var selector = new SettingSelector()
            {
                KeyFilter = "webhookrouter/routes/test/*"
            };

            var settings = client.GetConfigurationSettingsAsync(selector);

            await foreach (var setting in settings)
            {
                log.LogInformation("Setting {key} has value {value}.", setting.Key, setting.Value);
            }

            return new OkResult();
        }
    }
}
