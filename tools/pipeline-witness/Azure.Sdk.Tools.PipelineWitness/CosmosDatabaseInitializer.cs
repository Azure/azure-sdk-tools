using System.Threading;
using System.Threading.Tasks;

using Azure.Sdk.Tools.PipelineWitness.Configuration;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.PipelineWitness
{
    internal class CosmosDatabaseInitializer : IHostedService
    {
        private readonly CosmosClient cosmosClient;
        private readonly IOptions<PipelineWitnessSettings> options;

        public CosmosDatabaseInitializer(CosmosClient cosmosClient, IOptions<PipelineWitnessSettings> options)
        {
            this.cosmosClient = cosmosClient;
            this.options = options;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var settings = this.options.Value;

            Database database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(settings.CosmosDatabase, cancellationToken: cancellationToken);

            await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties 
                { 
                    Id = settings.CosmosAsyncLockContainer,
                    PartitionKeyPath = "/id",                    
                }, 
                cancellationToken: cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
