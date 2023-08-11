using System;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.Extensions.Configuration;
using Azure;
using Azure.AI.OpenAI;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Linq;
using Newtonsoft.Json;

namespace APIViewWeb.Managers
{
    public class CopilotCommentsManager : ICopilotCommentsManager
    {
        private readonly ICopilotCommentsRepository _copilotCommentsRepository;
        private readonly IConfiguration _configuration;
        private readonly OpenAIClient _openAIClient;

        public CopilotCommentsManager(
            ICopilotCommentsRepository copilotCommentsRepository,
            IConfiguration configuration
            )
        {
            _copilotCommentsRepository = copilotCommentsRepository;
            _configuration = configuration;

            _openAIClient = new OpenAIClient(
                new Uri(_configuration["OpenAI:Endpoint"]),
                new AzureKeyCredential(_configuration["OpenAI:Key"]));
        }

        public async Task<string> CreateDocumentAsync(string user, string badCode, string goodCode, string language, string comment, string[] guidelineIds)
        {
            var embedding = await GetEmbeddingsAsync(badCode);

            var document = new CopilotCommentModel()
            {
                BadCode = badCode,
                GoodCode = goodCode,
                Embedding = embedding,
                Language = language,
                Comment = comment,
                GuidelineIds = guidelineIds,
                ModifiedOn = DateTime.UtcNow,
                ModifiedBy = user
            };

            return await _copilotCommentsRepository.InsertDocumentAsync(document);
        }

        public async Task<UpdateResult> UpdateDocumentAsync(string user, string id, string badCode, string goodCode, string language, string comment, string[] guidelineIds)
        {
            var filter = GetIdFilter(id);

            var updateBuilder = Builders<CopilotCommentModel>.Update;
            var update = updateBuilder
                .Set("ModifiedOn", DateTime.UtcNow)
                .Set("ModifiedBy", user);

            if (goodCode != null)
            {
                update = update.Set("GoodCode", goodCode);
            }

            if (language != null)
            {
                update = update.Set("Language", language);
            }

            if (comment != null)
            {
                update = update.Set("Comment", comment);
            }

            if (guidelineIds != null)
            {
                update = update.Set("GuidelineIds", guidelineIds);
            }

            if (badCode != null)
            {
                var embedding = await GetEmbeddingsAsync(badCode);
                update = update
                    .Set("BadCode", badCode)
                    .Set("Embedding", embedding);
            }

            return await _copilotCommentsRepository.UpdateDocumentAsync(filter, update);
        }

        public Task DeleteDocumentAsync(string user, string id)
        {
            var filter = GetIdFilter(id);

            var updateBuilder = Builders<CopilotCommentModel>.Update;
            var update = updateBuilder
                .Set("IsDeleted", true)
                .Set("ModifiedOn", DateTime.UtcNow)
                .Set("ModifiedBy", user);

            return _copilotCommentsRepository.DeleteDocumentAsync(filter, update); 
        }

        public async Task<string> GetDocumentAsync(string id)
        {
            var filter = GetIdFilter(id);
            var document = await _copilotCommentsRepository.GetDocumentAsync(filter);

            var documentJson = JsonConvert.SerializeObject(document);

            return documentJson;
        }

        private FilterDefinition<CopilotCommentModel> GetIdFilter(string id)
        {
            return Builders<CopilotCommentModel>.Filter
                .Where(f => f.Id.ToString() == id && f.IsDeleted == false);
        }

        private async Task<float[]> GetEmbeddingsAsync(string badCode)
        {
        /*
         * Structure of Embeddings object
         *  {
         *      {
         *          "Data", 
         *          [
         *              {
         *                  "EmbeddingsItem", 
         *                  {
         *                      { "Embedding", [ "float1", "float2" ]},
         *                      { "Index", "1"}
         *                  }
         *              }
         *          ]
         *      },
         *      {
         *          "Usage", 
         *          {
         *              { "PromptTokens", "1"},
         *              { "TotalTokens", "2"}
         *          }
         *      }
         *  }
         */

            var options = new EmbeddingsOptions(badCode);
            var response = await _openAIClient.GetEmbeddingsAsync(_configuration["OpenAI:Model"], options);
            var embeddings = response.Value;

            var embedding = embeddings.Data[0].Embedding.ToArray();

            return embedding;
        }

        public BsonDocument EmbeddingToBson(Embeddings embedding)
        {
            var data = embedding.Data;

            var embeddingUsageBson = new BsonDocument
            {
                { "promptTokens", embedding.Usage.PromptTokens },
                { "TotalTokens", embedding.Usage.TotalTokens }
            };

            var embeddingData = new BsonArray();

            var embeddingBson = new BsonDocument
            {
                { "Data", embeddingData },
                { "Usage", embeddingUsageBson }
            };

            foreach (var embeddingItem in data)
            {
                var vectors = embeddingItem.Embedding;
                var vectorsBson = BsonArray.Create(vectors);

                var embeddingItemBson = new BsonDocument
                {
                    { "Embedding", vectorsBson },
                    { "Index", embeddingItem.Index }
                };

                embeddingData.Add(embeddingItemBson);
            }

            return embeddingBson;
        }
    }
}
