using Azure.Data.AppConfiguration;
using Azure.Identity;
using Azure.Sdk.Tools.GitHubIssues;
using Azure.Sdk.Tools.GitHubIssues.Reports;
using Azure.Sdk.Tools.GitHubIssues.Services.Configuration;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using System;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Azure.Sdk.Tools.GitHubIssues
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

            builder.Services.AddAzureClients(builder =>
            {
                builder.UseCredential(credential);
                var keyVaultUri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.vault.azure.net/");
                builder.AddSecretClient(keyVaultUri);
            });

            builder.Services.AddSingleton<ConfigurationClient>((provider) =>
            {
                var appConfigurationUri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.azconfig.io/");
                var configurationClient = new ConfigurationClient(appConfigurationUri, credential);
                return configurationClient;
            });


            builder.Services.AddLogging();
            builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
            builder.Services.AddSingleton<FindCustomerRelatedIssuesInvalidState>();
            builder.Services.AddSingleton<FindIssuesInBacklogMilestones>();
            builder.Services.AddSingleton<FindIssuesInPastDueMilestones>();
            builder.Services.AddSingleton<FindNewGitHubIssuesAndPRs>();
            builder.Services.AddSingleton<FindStalePRs>();
            builder.Services.AddSingleton<FindTestIssues>();
        }
    }
}