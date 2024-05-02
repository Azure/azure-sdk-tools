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
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Azure.Sdk.Tools.PipelineWitness
{
    public static class Startup
    {
        public static void Configure(WebApplicationBuilder builder)
        {
            PipelineWitnessSettings settings = new();
            IConfigurationSection settingsSection = builder.Configuration.GetSection("PipelineWitness");
            settingsSection.Bind(settings);

            builder.Services.AddApplicationInsightsTelemetry(builder.Configuration);
            builder.Services.AddApplicationInsightsTelemetryProcessor<BlobNotFoundTelemetryProcessor>();
            builder.Services.AddTransient<ITelemetryInitializer, ApplicationVersionTelemetryInitializer>();

            builder.Services.AddSingleton<TokenCredential, DefaultAzureCredential>();

            builder.Services.AddAzureClients(azureBuilder =>
            {
                azureBuilder.UseCredential(provider => provider.GetRequiredService<TokenCredential>());
                azureBuilder.AddCosmosServiceClient(new Uri(settings.CosmosAccountUri));
                azureBuilder.AddBlobServiceClient(new Uri(settings.BlobStorageAccountUri));
                azureBuilder.AddQueueServiceClient(new Uri(settings.QueueStorageAccountUri))
                    .ConfigureOptions(o => o.MessageEncoding = Storage.Queues.QueueMessageEncoding.Base64);
            });

            builder.Services.AddSingleton<IAsyncLockProvider>(provider => new CosmosAsyncLockProvider(provider.GetRequiredService<CosmosClient>(), settings.CosmosDatabase, settings.CosmosAsyncLockContainer));
            builder.Services.AddTransient(CreateVssConnection);

            builder.Services.AddLogging();
            builder.Services.AddTransient<BlobUploadProcessor>();
            builder.Services.AddTransient<Func<BlobUploadProcessor>>(provider => provider.GetRequiredService<BlobUploadProcessor>);

            builder.Services.Configure<PipelineWitnessSettings>(settingsSection);

            builder.Services.AddHostedService<BuildCompleteQueueWorker>(settings.BuildCompleteWorkerCount);
            builder.Services.AddHostedService<AzurePipelinesBuildDefinitionWorker>();
        }

        private static void AddHostedService<T>(this IServiceCollection services, int instanceCount) where T : class, IHostedService
        {
            for (int i = 0; i < instanceCount; i++)
            {
                services.AddSingleton<IHostedService, T>();
            }
        }

        private static void AddCosmosServiceClient<TBuilder>(this TBuilder builder, Uri serviceUri) where TBuilder : IAzureClientFactoryBuilderWithCredential
        {
            builder.RegisterClientFactory((CosmosClientOptions options, TokenCredential cred) => new CosmosClient(serviceUri.AbsoluteUri, cred, options));
        }

        private static VssConnection CreateVssConnection(IServiceProvider provider)
        {
            TokenCredential azureCredential = provider.GetRequiredService<TokenCredential>();
            TokenRequestContext tokenRequestContext = new(VssAadSettings.DefaultScopes);
            AccessToken token = azureCredential.GetToken(tokenRequestContext, CancellationToken.None);

            Uri organizationUrl = new("https://dev.azure.com/azure-sdk");
            VssAadCredential vssCredential = new(new VssAadToken("Bearer", token.Token));
            VssHttpRequestSettings settings = VssClientHttpRequestSettings.Default.Clone();

            return new VssConnection(organizationUrl, vssCredential, settings);
        }
    }
}
