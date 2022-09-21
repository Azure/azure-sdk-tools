using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Cosmos;

namespace Azure.Sdk.Tools.PipelineWitness.Services.WorkTokens
{
    public class CosmosAsyncLockProvider : IAsyncLockProvider
    {
        private readonly Container container;

        public CosmosAsyncLockProvider(CosmosClient cosmosClient, string databaseName, string containerName)
        {
            if (cosmosClient == null)
            {
                throw new ArgumentNullException(nameof(cosmosClient));
            }

            this.container = cosmosClient.GetContainer(databaseName, containerName);
        }

        public async Task<IAsyncLock> GetLockAsync(string id, TimeSpan duration, CancellationToken cancellationToken)
        {
            var partitionKey = new PartitionKey(id);

            ItemResponse<CosmosLockDocument> response;
            
            try
            {
                response = await this.container.ReadItemAsync<CosmosLockDocument>(id, partitionKey, cancellationToken: cancellationToken);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return await CreateLockAsync(id, duration, cancellationToken);
            }

            var existingLock = response.Resource;

            if (existingLock.Expiration >= DateTime.UtcNow)
            {
                return null;
            }
            
            try
            {
                response = await this.container.ReplaceItemAsync(
                    new CosmosLockDocument(id, duration),
                    id,
                    partitionKey,
                    new ItemRequestOptions { IfMatchEtag = response.ETag }, 
                    cancellationToken);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return new CosmosAsyncLock(id, response.ETag, duration, this.container);
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
            }

            return null;
        }

        private async Task<IAsyncLock> CreateLockAsync(string id, TimeSpan duration, CancellationToken cancellationToken)
        {
            try
            {
                var response = await this.container.CreateItemAsync(
                    new CosmosLockDocument(id, duration),
                    new PartitionKey(id), 
                    cancellationToken: cancellationToken);

                return new CosmosAsyncLock(id, response.ETag, duration, this.container);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
            }

            return null;
        }
    }
}
