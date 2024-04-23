using System;
using System.Threading;
using Azure.Core;
using Azure.Core.Extensions;
using Azure.Identity;
using Azure.Sdk.Tools.PipelineWitness.ApplicationInsights;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.Services;
using Azure.Sdk.Tools.PipelineWitness.Services.WorkTokens;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Azure.Sdk.Tools.PipelineWitness
{
    public static class Startup
    {
        public static void Configure(WebApplicationBuilder builder)
        {
            var azureCredential = new DefaultAzureCredential();
            var settings = new PipelineWitnessSettings();
            var settingsSection = builder.Configuration.GetSection("PipelineWitness");
            settingsSection.Bind(settings);

            builder.Services.AddApplicationInsightsTelemetry(builder.Configuration);
            builder.Services.AddApplicationInsightsTelemetryProcessor<BlobNotFoundTelemetryProcessor>();
            builder.Services.AddTransient<ITelemetryInitializer, ApplicationVersionTelemetryInitializer>();

            builder.Services.AddSingleton<TokenCredential>(azureCredential);

            builder.Services.AddAzureClients(builder =>
            {
                builder.UseCredential(provider => provider.GetRequiredService<TokenCredential>());
                builder.AddCosmosServiceClient(new Uri(settings.CosmosAccountUri));
                builder.AddBlobServiceClient(new Uri(settings.BlobStorageAccountUri));
                builder.AddQueueServiceClient(new Uri(settings.QueueStorageAccountUri))
                    .ConfigureOptions(o => o.MessageEncoding = Storage.Queues.QueueMessageEncoding.Base64);
            });

            builder.Services.AddSingleton<IAsyncLockProvider>(provider => new CosmosAsyncLockProvider(provider.GetRequiredService<CosmosClient>(), settings.CosmosDatabase, settings.CosmosAsyncLockContainer));
            builder.Services.AddSingleton(CreateVssConnection);
            builder.Services.AddVssClient<TestResultsHttpClient>();
            builder.Services.AddVssClient<BuildHttpClient>();

            builder.Services.AddLogging();
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<BlobUploadProcessor>();
            builder.Services.AddSingleton<BuildLogProvider>();

            builder.Services.Configure<PipelineWitnessSettings>(settingsSection);
            builder.Services.AddSingleton<TokenCredential, DefaultAzureCredential>();

            builder.Services.AddHostedService<BuildCompleteQueueWorker>(settings.BuildCompleteWorkerCount);
            builder.Services.AddHostedService<AzurePipelinesBuildDefinitionWorker>();
        }

        private static void AddHostedService<T>(this IServiceCollection services, int instanceCount) where T : class, IHostedService
        {
            for (var i = 0; i < instanceCount; i++)
            {
                services.AddSingleton<IHostedService, T>();
            }
        }

        private static void AddVssClient<T>(this IServiceCollection services) where T : class, IVssHttpClient
        {
            services.AddTransient(provider => provider.GetRequiredService<VssConnection>().GetClient<T>());
        }

        private static IAzureClientBuilder<CosmosClient, CosmosClientOptions> AddCosmosServiceClient<TBuilder>(this TBuilder builder, Uri serviceUri) where TBuilder : IAzureClientFactoryBuilderWithCredential
        {
            return builder.RegisterClientFactory((CosmosClientOptions options, TokenCredential cred) => new CosmosClient(serviceUri.AbsoluteUri, cred, options));

        }

        private static VssConnection CreateVssConnection(IServiceProvider provider)
        {
            var azureCredential = provider.GetRequiredService<TokenCredential>();
            TokenRequestContext tokenRequestContext = new(VssAadSettings.DefaultScopes);
            var token = azureCredential.GetToken(tokenRequestContext, CancellationToken.None);

            Uri organizationUrl = new("https://dev.azure.com/azure-sdk");
            VssAadCredential vssCredential = new(new VssAadToken("Bearer", token.Token));
            VssHttpRequestSettings settings = VssClientHttpRequestSettings.Default.Clone();

            return new VssConnection(organizationUrl, vssCredential, settings);
        }
    }
}
