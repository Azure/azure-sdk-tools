using CsvHelper;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using System;

namespace APIViewWeb.MiddleWare
{
    public class UITestsMiddleWare
    {
        private readonly RequestDelegate _next;
        public UITestsMiddleWare(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext httpContext, IConfiguration config)
        {
            var cosmosClient = new CosmosClient(config["CosmosEndpoint"], new DefaultAzureCredential());
            var dataBaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(config["CosmosDBName"]);
            _ = await dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Reviews", "/id");
            _ = await dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Comments", "/ReviewId");
            _ = await dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Profiles", "/id");
            _ = await dataBaseResponse.Database.CreateContainerIfNotExistsAsync("PullRequests", "/PullRequestNumber");
            _ = await dataBaseResponse.Database.CreateContainerIfNotExistsAsync("UsageSamples", "/ReviewId");
            _ = await dataBaseResponse.Database.CreateContainerIfNotExistsAsync("UserPreference", "/ReviewId");

            var blobServiceClient = new BlobServiceClient(new Uri(config["StorageAccountUrl"]), new DefaultAzureCredential());
            var blobCodeFileContainerClient = blobServiceClient.GetBlobContainerClient("codefiles");
            var blobOriginalContainerClient = blobServiceClient.GetBlobContainerClient("originals");
            var blobUsageSampleRepository = blobServiceClient.GetBlobContainerClient("usagesamples");
            var blobCommentsRepository = blobServiceClient.GetBlobContainerClient("comments");
            _ = await blobCodeFileContainerClient.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);
            _ = await blobOriginalContainerClient.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);
            _ = await blobUsageSampleRepository.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);
            _ = await blobCommentsRepository.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);

            await _next(httpContext);
        }
    }
}
