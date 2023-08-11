using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace APIViewWeb.Repositories
{
    public class CopilotCommentsRepository : ICopilotCommentsRepository
    {
        private readonly MongoClient _mongoClient;
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<CopilotCommentModel> _collection;

        public CopilotCommentsRepository(IConfiguration configuration)
        {
            _mongoClient = new MongoClient(configuration["CosmosMongoDB:ConnectionString"]);
            _database = _mongoClient.GetDatabase(configuration["CosmosMongoDB:DatabaseName"]);
            _collection = _database.GetCollection<CopilotCommentModel>(configuration["CosmosMongoDB:CollectionName"]);
        }

        public async Task<string> InsertDocumentAsync(CopilotCommentModel document)
        {
            await _collection.InsertOneAsync(document);
            return document.Id.ToString();
        }

        public async Task<UpdateResult> UpdateDocumentAsync(
            FilterDefinition<CopilotCommentModel> filter,
            UpdateDefinition<CopilotCommentModel> update)
        {
            return await _collection.UpdateOneAsync(filter, update);
        }

        public Task DeleteDocumentAsync(
            FilterDefinition<CopilotCommentModel> filter,
            UpdateDefinition<CopilotCommentModel> update)
        {
            return _collection.UpdateOneAsync(filter, update);
        }

        public Task<CopilotCommentModel> GetDocumentAsync(FilterDefinition<CopilotCommentModel> filter)
        {
            return _collection.Find(filter).FirstOrDefaultAsync();
        }
    }
}
