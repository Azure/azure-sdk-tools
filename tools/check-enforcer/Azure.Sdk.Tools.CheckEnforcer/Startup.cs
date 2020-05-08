using Azure.Sdk.Tools.CheckEnforcer;
using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddSingleton<IGlobalConfigurationProvider, GlobalConfigurationProvider>();
            builder.Services.AddSingleton<IGitHubClientProvider, GitHubClientProvider>();
            builder.Services.AddSingleton<IRepositoryConfigurationProvider, RepositoryConfigurationProvider>();
            builder.Services.AddSingleton<GitHubWebhookProcessor>();
            builder.Services.AddMemoryCache();
        }
    }
}
