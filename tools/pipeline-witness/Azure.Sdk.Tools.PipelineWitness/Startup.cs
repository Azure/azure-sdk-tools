using Azure.Cosmos;
using Azure.Sdk.Tools.PipelineWitness;
using Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

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

        public override void Configure(IFunctionsHostBuilder builder)
        {
            var websiteResourceGroupEnvironmentVariable = GetWebsiteResourceGroupEnvironmentVariable();

            builder.Services.AddAzureClients(builder =>
            {
                var keyVaultUri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.vault.azure.net/");
                builder.AddSecretClient(keyVaultUri);
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

            builder.Services.AddLogging();
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<RunProcessor>();
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
            builder.Services.AddSingleton<IFailureClassifier, CacheChunkOrderingFailureClassifier>();
        }
    }
}
