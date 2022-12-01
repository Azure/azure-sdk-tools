using CsvHelper;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

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
            var cosmosClient = new CosmosClient(config["Cosmos:ConnectionString"]);
            var dataBaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync("APIView");
            _ = await dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Reviews", "/id");
            _ = await dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Comments", "/ReviewId");
            _ = await dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Profiles", "/id");
            _ = await dataBaseResponse.Database.CreateContainerIfNotExistsAsync("PullRequests", "/PullRequestNumber");
            _ = await dataBaseResponse.Database.CreateContainerIfNotExistsAsync("UsageSamples", "/ReviewId");
            _ = await dataBaseResponse.Database.CreateContainerIfNotExistsAsync("UserPreference", "/ReviewId");

            var blobCodeFileContainerClient = new BlobContainerClient(config["Blob:ConnectionString"], "codefiles");
            var blobOriginalContainerClient = new BlobContainerClient(config["Blob:ConnectionString"], "originals");
            var blobUsageSampleRepository = new BlobContainerClient(config["Blob:ConnectionString"], "usagesamples");
            var blobCommentsRepository = new BlobContainerClient(config["Blob:ConnectionString"], "comments");
            _ = await blobCodeFileContainerClient.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);
            _ = await blobOriginalContainerClient.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);
            _ = await blobUsageSampleRepository.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);
            _ = await blobCommentsRepository.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);

            await _next(httpContext);
        }
    }
}
