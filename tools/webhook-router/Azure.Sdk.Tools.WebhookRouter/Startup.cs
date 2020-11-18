using Azure.Identity;
using Azure.Sdk.Tools.WebhookRouter;
using Azure.Sdk.Tools.WebhookRouter.Routing;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Azure.Sdk.Tools.WebhookRouter
{
    public class Startup : FunctionsStartup
    {

        private string GetWebsiteResourceGroupEnvironmentVariable()
        {
            var websiteResourceGroupEnvironmentVariable = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP");
            return websiteResourceGroupEnvironmentVariable;
        }

        private Uri GetKeyVaultUri()
        {
            var websiteResourceGroupEnvironmentVariable = GetWebsiteResourceGroupEnvironmentVariable();
            var uri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.vault.azure.net/");
            return uri;
        }

        private Uri GetConfigurationUri()
        {
            var websiteResourceGroupEnvironmentVariable = GetWebsiteResourceGroupEnvironmentVariable();
            var uri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.azconfig.io/");
            return uri;
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddAzureClients((builder) =>
            {
                builder.AddConfigurationClient(GetConfigurationUri());
                builder.AddSecretClient(GetKeyVaultUri());
            });

            builder.Services.AddSingleton<IRouter, Router>();
            builder.Services.AddMemoryCache();
        }
    }
}
