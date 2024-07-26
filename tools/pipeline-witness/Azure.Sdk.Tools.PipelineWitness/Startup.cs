using System;
using System.Threading;
using Azure.Core;
using Azure.Core.Extensions;
using Azure.Identity;
using Azure.Sdk.Tools.PipelineWitness.ApplicationInsights;
using Azure.Sdk.Tools.PipelineWitness.AzurePipelines;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.GitHubActions;
using Azure.Sdk.Tools.PipelineWitness.Services.WorkTokens;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Octokit;

namespace Azure.Sdk.Tools.PipelineWitness;

public static class Startup
{
    public static void Configure(WebApplicationBuilder builder)
    {
        IConfigurationSection settingsSection = builder.Configuration.GetSection("PipelineWitness");
        PipelineWitnessSettings settings = new();
        settingsSection.Bind(settings);

        builder.Services.AddLogging();

        builder.Services.Configure<PipelineWitnessSettings>(settingsSection);
        builder.Services.AddSingleton<ISecretClientProvider, SecretClientProvider>();
        builder.Services.AddSingleton<IPostConfigureOptions<PipelineWitnessSettings>, PostConfigureKeyVaultSettings<PipelineWitnessSettings>>();

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

        builder.Services.AddTransient<AzurePipelinesProcessor>();
        builder.Services.AddTransient<BuildCompleteQueue>();
        builder.Services.AddHostedService<BuildCompleteQueueWorker>(settings.BuildCompleteWorkerCount);

        builder.Services.AddSingleton<ICredentialStore, GitHubCredentialStore>();
        builder.Services.AddTransient<GitHubActionProcessor>();
        builder.Services.AddTransient<RunCompleteQueue>();
        builder.Services.AddHostedService<RunCompleteQueueWorker>(settings.GitHubActionRunsWorkerCount);

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
        Azure.Core.AccessToken token = azureCredential.GetToken(tokenRequestContext, CancellationToken.None);

        Uri organizationUrl = new("https://dev.azure.com/azure-sdk");
        VssAadCredential vssCredential = new(new VssAadToken("Bearer", token.Token));
        VssHttpRequestSettings settings = VssClientHttpRequestSettings.Default.Clone();

        return new VssConnection(organizationUrl, vssCredential, settings);
    }
}
