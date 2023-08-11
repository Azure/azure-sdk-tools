using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;
using System.Text.Json;

namespace APIViewWeb.Repositories
{
    public class CopilotCommentsRepository : ICopilotCommentsRepository
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Database _database;
        private readonly Microsoft.Azure.Cosmos.Container _container;

        public CopilotCommentsRepository(IConfiguration configuration)
        {
            _cosmosClient = new CosmosClient(configuration["CosmosUITest:ConnectionString"]);
            _database = _cosmosClient.GetDatabase(configuration["CosmosUITest:DatabaseName"]);
            _container = _database.GetContainer(configuration["CosmosUITest:ContainerName"]);
        }

        public async Task InsertDocumentAsync(CopilotCommentModel document)
        {
            await _container.CreateItemAsync<CopilotCommentModel>(item: document, partitionKey: new PartitionKey(document.Language));
        }

        public async Task<CopilotCommentModel> UpdateDocumentAsync(string id, string language, IEnumerable<PatchOperation> updates)
        {
            var readResponse = await _container.PatchItemAsync<CopilotCommentModel>(
                id: id,
                partitionKey: new PartitionKey(language),
                patchOperations: updates.ToList());
            return readResponse.Resource;
        }

        public Task DeleteDocumentAsync(string id, string language, IEnumerable<PatchOperation> updates)
        {
            return _container.PatchItemAsync<CopilotCommentModel>(
                id: id,
                partitionKey: new PartitionKey(language),
                patchOperations: updates.ToList());
        }

        public async Task<CopilotCommentModel> GetDocumentAsync(string id, string language)
        {
            var readResponse = await _container.ReadItemAsync<CopilotCommentModel>(id, partitionKey: new PartitionKey(language));
            return readResponse.Resource;
        }

        public IEnumerable<CopilotCommentModel> SearchLanguage(string language)
        {
            var queryable = _container.GetItemLinqQueryable<CopilotCommentModel>(allowSynchronousQueryExecution: true);
            var matches = queryable.Where(p => p.Language == language);
            return matches.ToList();
        }
    }
}
