using Azure.Data.AppConfiguration;
using Azure.Identity;
using Azure.Sdk.Tools.PullRequestLabeler;
using Azure.Sdk.Tools.PullRequestLabeler.Services.GitHubIntegration;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Azure.Sdk.Tools.PullRequestLabeler
{
    public class Startup : FunctionsStartup
    {
        private string GetWebsiteResourceGroupEnvironmentVariable()
        {
            var websiteResourceGroupEnvironmentVariable = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP");
            return websiteResourceGroupEnvironmentVariable;
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            var websiteResourceGroupEnvironmentVariable = GetWebsiteResourceGroupEnvironmentVariable();

            builder.Services.AddAzureClients(builder =>
            {
                var keyVaultUri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.vault.azure.net");
                builder.AddKeyClient(keyVaultUri);
                builder.AddCryptographyClient(keyVaultUri);
            });

            builder.Services.AddSingleton<ConfigurationClient>(provider =>
            {
                var credential = new DefaultAzureCredential();
                var appConfigurationUri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.azconfig.io");
                var client = new ConfigurationClient(appConfigurationUri, credential);
                return client;
            });

            builder.Services.AddLogging();
            builder.Services.AddCac
            builder.Services.AddSingleton<IGitHubIntegrationService, GitHubInstallationService>();
        }
    }
}
