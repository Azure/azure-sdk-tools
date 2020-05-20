using Azure.Cosmos;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness
{
    public class RunProcessor
    {
        public RunProcessor(ILogger<RunProcessor> logger, IMemoryCache cache, HttpClient httpClient)
        {
            this.logger = logger;
            this.cache = cache;
            this.httpClient = httpClient;
        }

        private ILogger<RunProcessor> logger;
        private IMemoryCache cache;
        private HttpClient httpClient;

        private AuthenticationHeaderValue GetAuthenticationHeader(string azureDevOpsPersonalAccessToken)
        {
            var usernameAndPassword = $"nobody:{azureDevOpsPersonalAccessToken}";
            var usernameAndPasswordBytes = Encoding.ASCII.GetBytes(usernameAndPassword);
            var encodedUsernameAndPassword = Convert.ToBase64String(usernameAndPasswordBytes);
            var header = new AuthenticationHeaderValue("Basic", encodedUsernameAndPassword);
            return header;
        }

        public async Task ProcessRunAsync(Uri runUri)
        {
            try
            {
                logger.LogInformation("Processing run: {runUri}", runUri);
                var content = await GetContentFromAzureDevOpsAsync(runUri);
                var document = JsonDocument.Parse(content);
                var record = new AzurePipelinesRun(document);

                var container = await GetItemContainerAsync("scratch");

                logger.LogInformation("Upserting run: {runUri}", runUri);
                await container.UpsertItemAsync(record);
            }
            catch (ContentNotFoundException ex)
            {
                logger.LogWarning(ex, "Run information was not found, possibly a PR run that was cancelled and removed?");
                return;
            }
        }

        private async Task<CosmosClient> GetCosmosClientAsync()
        {
            var websiteResourceGroupEnvironmentVariable = GetWebsiteResourceGroupEnvironmentVariable();
            var accountEndpoint = $"https://{websiteResourceGroupEnvironmentVariable}.documents.azure.com";
            var cosmosDbPrimaryAuthorizationKey = await GetCosmosDbPrimaryAuthorizationKeyAsync();
            var cosmosClient = new CosmosClient(accountEndpoint, cosmosDbPrimaryAuthorizationKey);
            return cosmosClient;
        }

        private async Task<CosmosContainer> GetItemContainerAsync(string containerName)
        {
            var client = await GetCosmosClientAsync();
            var database = client.GetDatabase("records");
            var container = client.GetContainer(database.Id, containerName);
            return container;
        }

        private async Task<string> GetCosmosDbPrimaryAuthorizationKeyAsync()
        {
            var cosmosDbPrimaryAuthorizationKey = await GetSecretAsync("cosmosdb-primary-authorization-key");
            return cosmosDbPrimaryAuthorizationKey;
        }

        private bool IsContentUrlValid(Uri contentUri)
        {
            return contentUri.Host == "dev.azure.com";
        }

        private async Task<string> GetContentFromAzureDevOpsAsync(Uri contentUri)
        {
            if (!IsContentUrlValid(contentUri)) throw new InvalidOperationException($"Invalid contentUrl: {contentUri}");

            // TODO: Add content here to cache and archive these payloads.
            var request = new HttpRequestMessage(HttpMethod.Get, contentUri);
            var azureDevOpsPersonalAccessToken = await GetAzureDevOpsPersonalAccessTokenAsync();
            request.Headers.Authorization = GetAuthenticationHeader(azureDevOpsPersonalAccessToken);

            logger.LogInformation("Downloading content from: {contentUri}", contentUri);
            var response = await httpClient.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ContentNotFoundException(contentUri);
            }
            else if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new ContentDownloadException($"Failed to download content (status {response.StatusCode}): {contentUri}");
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
        }

        private string GetWebsiteResourceGroupEnvironmentVariable()
        {
            logger.LogInformation("Fetching WEBSITE_RESOURCE_GROUP environment variable.");
            var websiteResourceGroupEnvironmentVariable = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP");
            logger.LogInformation("WEBSITE_RESOURCE_GROUP environemnt variable was: {websiteResourceGroupEnvironmentVariable}", websiteResourceGroupEnvironmentVariable);
            return websiteResourceGroupEnvironmentVariable;
        }

        private async Task<string> GetAzureDevOpsPersonalAccessTokenAsync()
        {
            logger.LogInformation("Fetching Azure DevOps personal access token from KeyVault.");
            var azureDevOpsPersonalAccessToken = await GetSecretAsync("azure-devops-personal-access-token");
            return azureDevOpsPersonalAccessToken;
        }

        private async Task<string> GetSecretAsync(string secretName)
        {
            var secretCacheKey = $"{secretName}_secretCacheKey";

            var client = GetSecretClient();
            var secret = await cache.GetOrCreateAsync(secretCacheKey, async (entry) =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                KeyVaultSecret secret = await client.GetSecretAsync(secretName);
                return secret.Value;
            });

            return secret;
        }

        private SecretClient GetSecretClient()
        {
            var secretClientCacheKey = "secretClientCacheKey";

            var secretClient = cache.GetOrCreate(secretClientCacheKey, (entry) =>
            {
                var websiteResourceGroupEnvironmentVariable = GetWebsiteResourceGroupEnvironmentVariable();
                var uri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.vault.azure.net/");
                var credential = new DefaultAzureCredential();
                var client = new SecretClient(uri, credential);
                return client;
            });

            return secretClient;
        }
    }
}
