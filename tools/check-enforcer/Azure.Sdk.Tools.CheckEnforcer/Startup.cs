using Azure.Data.AppConfiguration;
using Azure.Identity;
using Azure.Sdk.Tools.CheckEnforcer;
using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Azure.Sdk.Tools.CheckEnforcer
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

            var credential = new DefaultAzureCredential();

            builder.Services.AddAzureClients((builder) =>
            {
                builder.UseCredential(credential);

                var keyVaultUri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.vault.azure.net");
                builder.AddSecretClient(keyVaultUri);
                
                var configurationUri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.azconfig.io/");
                builder.AddConfigurationClient(configurationUri);

                // To inject the cryptography client with the extension helpers
                // here we need to first find the Key ID.
                var keyClient = new KeyClient(keyVaultUri, credential);
                KeyVaultKey key = keyClient.GetKey("github-app-private-key");
                builder.AddCryptographyClient(key.Id);
            });

            builder.Services.AddSingleton<IGlobalConfigurationProvider, GlobalConfigurationProvider>();
            builder.Services.AddSingleton<IGitHubClientProvider, GitHubClientProvider>();
            builder.Services.AddSingleton<IRepositoryConfigurationProvider, RepositoryConfigurationProvider>();
            builder.Services.AddSingleton<GitHubWebhookProcessor>();
            builder.Services.AddMemoryCache();
        }
    }
}
