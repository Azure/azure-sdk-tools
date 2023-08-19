using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;
using APIViewWeb.Models;
using System;
using Azure.Search.Documents;
using Azure;
using Azure.Search.Documents.Models;

namespace APIViewWeb.Repositories
{
    public class CopilotCommentsRepository : ICopilotCommentsRepository
    {
        private readonly IConfiguration _configuration;
        private readonly CosmosClient _cosmosClient;
        private readonly Database _database;
        private readonly Microsoft.Azure.Cosmos.Container _container;
        private readonly Azure.Search.Documents.SearchClient _searchClient;

        public CopilotCommentsRepository(IConfiguration configuration)
        {
            _configuration = configuration;
            _cosmosClient = new CosmosClient(configuration["CosmosUITest:ConnectionString"]);
            _database = _cosmosClient.GetDatabase(configuration["CosmosUITest:DatabaseName"]);
            _container = _database.GetContainer(configuration["CosmosUITest:ContainerName"]);
            _searchClient = new Azure.Search.Documents.SearchClient(new Uri(configuration["CognitiveSearch:Endpoint"]), "embedding-search-index", new AzureKeyCredential(configuration["CognitiveSearch:Key"]));
        }

        public async Task InsertDocumentAsync(CopilotCommentModel document)
        {
            var embedding = new SearchEmbedding { Id = document.Id, Embedding = document.Embedding, Language = document.Language };
            await _searchClient.MergeOrUploadDocumentsAsync(new[] { embedding });
            await _container.CreateItemAsync(item: document, partitionKey: new PartitionKey(document.Language));
        }

        public async Task UpdateDocumentAsync(CopilotCommentModel document)
        {
            var embedding = new SearchEmbedding { Id = document.Id, Embedding = document.Embedding, Language = document.Language };
            await _searchClient.MergeOrUploadDocumentsAsync(new[] { embedding });
            await _container.ReplaceItemAsync<CopilotCommentModel>(document, document.Id, new PartitionKey(document.Language));
        }

        /* 
         * Delete is a soft delete operation for CosmosDB - it sets the is_deleted flag to true.
         * It is a hard delete for Cognitive Search to omit it from search.
         */ 
        public async Task DeleteDocumentAsync(string id, string language, string user)
        {
            await _searchClient.DeleteDocumentsAsync("id", new[] { id });
            var document = await GetDocumentAsync(id, language);
            document.IsDeleted = true;
            document.ModifiedOn = DateTime.UtcNow;
            document.ModifiedBy = user;
        }

        public async Task<CopilotCommentModel> GetDocumentAsync(string id, string language)
        {
            var response = await _container.ReadItemAsync<CopilotCommentModel>(id, new PartitionKey(language));
            return response.Resource;
        }

        public async Task<IEnumerable<CopilotSearchModel>> SimilaritySearchAsync(string language, float[] embedding, float threshold, int limit)
        {
            var searchOptions = new SearchOptions()
            {
                Filter = SearchFilter.Create($"language eq {language}"),
                Vectors = { new() { Value = embedding, KNearestNeighborsCount = limit, Fields = { "embedding" } } }
            };
            Azure.Response<SearchResults<SearchEmbedding>> response = await _searchClient.SearchAsync<SearchEmbedding>(searchText: null, searchOptions);
            List<CopilotSearchModel> results = new List<CopilotSearchModel>();

            await foreach (SearchResult<SearchEmbedding> result in response.Value.GetResultsAsync())
            {
                SearchEmbedding doc = result.Document;
                results.Add(new CopilotSearchModel((float)result.Score, await GetDocumentAsync(doc.Id, language)));
            }
            return results;
        }
    }
}
