using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Azure.Sdk.Tools.PipelineWitness.Services.WorkTokens
{
    class CosmosAsyncLock : IAsyncLock
    {
        private readonly string id;
        private readonly PartitionKey partitionKey;
        private readonly Container container;
        private string etag;

        public CosmosAsyncLock(string id, string etag, Container container)
        {
            this.id = id;
            this.partitionKey = new PartitionKey(id);
            this.etag = etag;
            this.container = container;
        }

        public bool ReleaseOnDispose { get; set; }

        public async ValueTask DisposeAsync()
        {
            if (ReleaseOnDispose)
            {
                try
                {
                    await this.container.DeleteItemAsync<CosmosLockDocument>(this.id, this.partitionKey, new ItemRequestOptions { IfMatchEtag = this.etag });
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                }
            }
        }

        public async Task<bool> TryExtendAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            try
            {
                ItemResponse<CosmosLockDocument> response = await this.container.ReplaceItemAsync(
                    new CosmosLockDocument(this.id, duration),
                    this.id,
                    this.partitionKey,
                    new ItemRequestOptions { IfMatchEtag = this.etag },
                    cancellationToken);

                if (response?.StatusCode == HttpStatusCode.OK)
                {
                    this.etag = response.ETag;
                    return true;
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
            }

            return false;
        }
    }
}
