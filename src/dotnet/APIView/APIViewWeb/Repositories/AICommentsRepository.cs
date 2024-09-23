using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;
using APIViewWeb.Models;
using System;
using Azure.Search.Documents;
using Azure;
using Azure.Search.Documents.Models;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Repositories
{
    public class AICommentsRepository : IAICommentsRepository
    {
        private readonly Container _aiCommentContainer;
        private readonly SearchClient _searchClient;

        public AICommentsRepository(IConfiguration configuration, CosmosClient cosmosClient)
        {
            _aiCommentContainer = cosmosClient.GetContainer("APIView", "AIComments");
            _searchClient = new SearchClient(new Uri(configuration["CognitiveSearch:Endpoint"]), "ai-comments-search-index", new AzureKeyCredential(configuration["CognitiveSearch:Key"]));
        }

        public async Task UpsertAICommentAsync(AICommentModel aiCommentModel)
        {
            var Embedding = new SearchEmbedding { Id = aiCommentModel.Id, Embedding = aiCommentModel.Embedding, Language = aiCommentModel.Language };
            await _searchClient.MergeOrUploadDocumentsAsync(new[] { Embedding });
            await _aiCommentContainer.UpsertItemAsync(item: aiCommentModel, partitionKey: new PartitionKey(aiCommentModel.Id));
        }

        /* 
         * Delete is a soft delete operation for CosmosDB - it sets the is_deleted flag to true.
         * It is a hard delete for Cognitive Search to omit it from search.
         */ 
        public async Task DeleteAICommentAsync(string id, string user)
        {
            await _searchClient.DeleteDocumentsAsync("id", new[] { id });
            var aiCommentModel = await GetAICommentAsync(id);
            aiCommentModel.ChangeHistory.Add(new AICommentChangeHistoryModel()
            {
                ChangeAction = AICommentChangeAction.Deleted,
                ChangedBy = user,
                ChangedOn = DateTime.UtcNow
            });
            await _aiCommentContainer.UpsertItemAsync(item: aiCommentModel, partitionKey: new PartitionKey(aiCommentModel.Id));
        }

        public async Task<AICommentModel> GetAICommentAsync(string id)
        {
            return await _aiCommentContainer.ReadItemAsync<AICommentModel>(id, new PartitionKey(id));
        }

        public async Task<IEnumerable<AICommentModelForSearch>> SimilaritySearchAsync(AICommentDTOForSearch aiCommentDTOForSearch, float[] embedding)
        {
            var searchOptions = new SearchOptions()
            {
                Filter = SearchFilter.Create($"language eq {aiCommentDTOForSearch.Language}"),
                Vectors = { new() { Value = embedding, KNearestNeighborsCount = aiCommentDTOForSearch.Limit, Fields = { "embedding" } } }
            };
            var response = await _searchClient.SearchAsync<SearchEmbedding>(searchText: null, searchOptions);
            List<AICommentModelForSearch> results = new List<AICommentModelForSearch>();

            await foreach (SearchResult<SearchEmbedding> result in response.Value.GetResultsAsync())
            {
                SearchEmbedding doc = result.Document;
                results.Add(new AICommentModelForSearch((float)result.Score, await GetAICommentAsync(doc.Id)));
            }
            return results;
        }
    }
}
