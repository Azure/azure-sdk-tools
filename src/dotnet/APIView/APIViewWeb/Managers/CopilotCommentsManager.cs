using System;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.Extensions.Configuration;
using Azure;
using Azure.AI.OpenAI;
using MongoDB.Bson;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;

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

        public async Task<string> CreateDocumentAsync(string language, string badCode, string goodCode, string comment, string[] guidelineIds, string user)
        {
            var embedding = await GetEmbeddingsAsync(badCode);

            var document = new CopilotCommentModel()
            {
                Language = language,
                BadCode = badCode,
                GoodCode = goodCode,
                Embedding = embedding,
                Comment = comment,
                GuidelineIds = guidelineIds,
                ModifiedOn = DateTime.UtcNow,
                ModifiedBy = user
            };

            await _copilotCommentsRepository.InsertDocumentAsync(document);

            document.Embedding = null;
            return document.ToJson();
        }

        public async Task<CopilotCommentModel> UpdateDocumentAsync(string id, string language, string badCode, string goodCode, string comment, string[] guidelineIds, string user)
        {
            var updates = new List<PatchOperation>
            {
                PatchOperation.Set("/modified_on", DateTime.UtcNow),
                PatchOperation.Set("/modified_by", user),
            };

            if (language != null)
            {
                updates.Add(PatchOperation.Set("/language", language));
            }

            if (badCode != null)
            {
                var embedding = await GetEmbeddingsAsync(badCode);
                updates.Add(PatchOperation.Set("/bad_code", badCode));
                updates.Add(PatchOperation.Set("/embedding", embedding));
            }

            if (goodCode != null)
            {
                updates.Add(PatchOperation.Set("/good_code", goodCode));
            }

            if (comment != null)
            {
                updates.Add(PatchOperation.Set("/comment", comment));
            }

            if (guidelineIds != null)
            {
                updates.Add(PatchOperation.Set("/guideline_ids", guidelineIds));
            }

            return await _copilotCommentsRepository.UpdateDocumentAsync(id, language, updates);
        }

        public Task DeleteDocumentAsync(string id, string language, string user)
        {
            var updates = new List<PatchOperation>
            {
                PatchOperation.Set("/is_deleted", true),
                PatchOperation.Set("/modified_on", DateTime.UtcNow),
                PatchOperation.Set("/modified_by", user),
            };

            return _copilotCommentsRepository.DeleteDocumentAsync(id, language, updates); 
        }

        public async Task<string> GetDocumentAsync(string id, string language)
        {
            var document = await _copilotCommentsRepository.GetDocumentAsync(id, language);
            var documentJson = JsonConvert.SerializeObject(document);
            return documentJson;
        }

        public async Task<string> SearchDocumentsAsync(string language, string badCode, float threshold, int limit)
        {
            var embeddings = await GetEmbeddingsAsync(badCode);
            var documents = _copilotCommentsRepository.SearchLanguage(language);

            var searchResults = new List<CopilotSearchModel>();
            foreach (var document in documents)
            {
                var similarity = CosineSimilarity(document.Embedding, embeddings);
                if (similarity < threshold)
                    continue;
                searchResults.Add(new CopilotSearchModel(similarity, document));
            }

            var topResults = searchResults.OrderByDescending(item => item.similarity).Take(limit);
            return JsonConvert.SerializeObject(topResults);
        }

        public static float CosineSimilarity(float[] vec1, float[] vec2)
        {
            if (vec1.Length != vec2.Length)
                throw new ArgumentException("Vectors must have the same dimension");

            var dotProduct = 0.0;
            var norm1 = 0.0;
            var norm2 = 0.0;

            for (int i = 0; i < vec1.Length; i++)
            {
                dotProduct += vec1[i] * vec2[i];
                norm1 += Math.Pow(vec1[i], 2);
                norm2 += Math.Pow(vec2[i], 2);
            }

            var resultDouble = dotProduct / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
            return ((float)resultDouble);
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
    }
}
