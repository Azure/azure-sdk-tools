using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;
using APIViewWeb.Models;
using System;

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

        public async Task UpdateDocumentAsync(CopilotCommentModel document)
        {
            await _container.ReplaceItemAsync<CopilotCommentModel>(document, document.Id, new PartitionKey(document.Language));
        }

        // Delete is a soft delete operation - it sets the is_deleted flag to true.
        public async Task DeleteDocumentAsync(string id, string language, string user)
        {
            var document = await GetDocumentAsync(id, language);
            document.IsDeleted = true;
            document.ModifiedOn = DateTime.UtcNow;
            document.ModifiedBy = user;
            await UpdateDocumentAsync(document);
        }

        public async Task<CopilotCommentModel> GetDocumentAsync(string id, string language)
        {
            var response = await _container.ReadItemAsync<CopilotCommentModel>(id, new PartitionKey(language));
            return response.Resource;
        }

        public async Task<IEnumerable<CopilotCommentModel>> SearchLanguage(string language)
        {
            var matches = new List<CopilotCommentModel>();
            var query = $"SELECT * FROM CopilotComments c WHERE c.language = '{language}'";
            var itemQueryIterator = _container.GetItemQueryIterator<CopilotCommentModel>(query);
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                matches.AddRange(result.Resource);
            }
            return matches;
        }
    }
}
