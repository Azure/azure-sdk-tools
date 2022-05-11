using System;

using Azure.Cosmos;
using Azure.Sdk.Tools.PipelineWitness;
using Azure.Sdk.Tools.PipelineWitness.Services;
using Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Microsoft.ApplicationInsights.Extensibility;
using Azure.Sdk.Tools.PipelineWitness.ApplicationInsights;
using Microsoft.Extensions.Azure;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Azure.Sdk.Tools.PipelineWitness
{
    public class Startup : FunctionsStartup
    {
        private string GetWebsiteResourceGroupEnvironmentVariable()
        {
            var websiteResourceGroupEnvironmentVariable = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP");
            return websiteResourceGroupEnvironmentVariable;
        }

        private string GetAzureWebJobsStorageEnvironmentVariable()
        {
            var value = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            return value;
        }

        private string GetBuildBlobStorageEnvironmentVariable()
        {
            var environmentVariable = Environment.GetEnvironmentVariable("BUILD_BLOB_STORAGE_URI");
            return environmentVariable;
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            var websiteResourceGroupEnvironmentVariable = GetWebsiteResourceGroupEnvironmentVariable();
            var buildBlobStorageUri = GetBuildBlobStorageEnvironmentVariable();
            var azureWebJobStorageConnectionString = GetAzureWebJobsStorageEnvironmentVariable();

            builder.Services.AddAzureClients(builder =>
            {
                var keyVaultUri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.vault.azure.net/");
                builder.AddSecretClient(keyVaultUri);

                builder.AddBlobServiceClient(new Uri(buildBlobStorageUri));
                builder.AddQueueServiceClient(azureWebJobStorageConnectionString)
                    .ConfigureOptions(o => o.MessageEncoding = Storage.Queues.QueueMessageEncoding.Base64);
            });

            builder.Services.AddSingleton<CosmosClient>(provider =>
            {
                var secretClient = provider.GetService<SecretClient>();
                KeyVaultSecret secret = secretClient.GetSecret("cosmosdb-primary-authorization-key");
                var accountEndpoint = $"https://{websiteResourceGroupEnvironmentVariable}.documents.azure.com";

                // Let's see how this goes. Been having trouble with Cosmos and
                // and TCP connection limits in Azure Functions. If this persists
                // after this refactoring then the next step is to either switch
                // to gateway mode or limit the direct connections somehow.
                var cosmosClient = new CosmosClient(
                    accountEndpoint,
                    secret.Value,
                    new CosmosClientOptions()
                    {
                        Diagnostics = {
                            IsLoggingEnabled = false // HACK: https://github.com/Azure/azure-cosmos-dotnet-v3/issues/1592 
                                                     //       It should be safe to remove these options post 4.0.0-preview.3
                        }
                    }
                    );

                return cosmosClient;
            });

            builder.Services.AddSingleton<VssConnection>(provider =>
            {
                var secretClient = provider.GetService<SecretClient>();
                KeyVaultSecret secret = secretClient.GetSecret("azure-devops-personal-access-token");
                var credential = new VssBasicCredential("nobody", secret.Value);
                var connection = new VssConnection(new Uri("https://dev.azure.com/azure-sdk"), credential);
                return connection;
            });

            builder.Services.AddSingleton(provider => provider.GetRequiredService<VssConnection>().GetClient<ProjectHttpClient>());
            builder.Services.AddSingleton(provider => provider.GetRequiredService<VssConnection>().GetClient<BuildHttpClient>());
            builder.Services.AddSingleton(provider => provider.GetRequiredService<VssConnection>().GetClient<TestResultsHttpClient>());

            builder.Services.AddLogging();
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<RunProcessor>();
            builder.Services.AddSingleton<BlobUploadProcessor>();
            builder.Services.AddSingleton<BuildLogProvider>();
            builder.Services.AddSingleton<IFailureAnalyzer, FailureAnalyzer>();
            builder.Services.AddSingleton<IFailureClassifier, AzuriteInstallFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, CancelledTaskClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, CosmosDbEmulatorStartFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, AzurePipelinesPoolOutageClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, PythonPipelineTestFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, JavaScriptLiveTestFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, TestResourcesDeploymentFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, DotnetPipelineTestFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, JavaPipelineTestFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, JsSamplesExecutionFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, JsDevFeedPublishingFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, DownloadSecretsFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, GitCheckoutFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, AzuriteInstallFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, MavenBrokenPipeFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, CodeSigningFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, AzureArtifactsServiceUnavailableClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, DnsResolutionFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, CacheFailureClassifier>();
            builder.Services.AddTransient<ITelemetryInitializer, NotFoundTelemetryInitializer>();
            builder.Services.AddTransient<ITelemetryInitializer, ApplicationVersionTelemetryInitializer<Startup>>();
            builder.Services.Configure<PipelineWitnessSettings>(builder.GetContext().Configuration);
        }
    }
}
