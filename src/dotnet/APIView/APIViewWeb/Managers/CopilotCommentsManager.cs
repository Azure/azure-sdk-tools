using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Linq;
using Azure;
using Azure.AI.OpenAI;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using System.Text.Json;

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

        public async Task<string> CreateDocumentAsync(string language, string badCode, string goodCode, string comment, string guidelineIds, string user)
        {
            var document = new CopilotCommentModel()
            {
                Language = language,
                BadCode = badCode,
                GoodCode = goodCode,
                Embedding = await GetEmbeddingsAsync(badCode),
                Comment = comment,
                GuidelineIds = SplitGuidelineIds(guidelineIds),
                ModifiedOn = DateTime.UtcNow,
                ModifiedBy = user
            };

            await _copilotCommentsRepository.InsertDocumentAsync(document);
            return GetDocumentJsonWithoutEmbedding(document);
        }

        public async Task<string> UpdateDocumentAsync(string id, string language, string badCode, string goodCode, string comment, string guidelineIds, string user)
        {
            var document = await _copilotCommentsRepository.GetDocumentAsync(id, language);

            document.ModifiedOn = DateTime.UtcNow;
            document.ModifiedBy = user;

            if (language != null)
            {
                document.Language = language;
            }

            if (badCode != null)
            {
                var embedding = await GetEmbeddingsAsync(badCode);
                document.BadCode = badCode;
                document.Embedding = embedding;
            }

            if (goodCode != null)
            {
                document.GoodCode = goodCode;
            }

            if (comment != null)
            {
                document.Comment = comment;
            }

            if (guidelineIds != null)
            {
                document.GuidelineIds = SplitGuidelineIds(guidelineIds);
            }

            await _copilotCommentsRepository.UpdateDocumentAsync(document);
            return GetDocumentJsonWithoutEmbedding(document);
        }

        public Task DeleteDocumentAsync(string id, string language, string user)
        {
            return _copilotCommentsRepository.DeleteDocumentAsync(id, language, user); 
        }

        public async Task<string> GetDocumentAsync(string id, string language)
        {
            var document = await _copilotCommentsRepository.GetDocumentAsync(id, language);
            return GetDocumentJsonWithoutEmbedding(document);
        }

        public async Task<string> SearchDocumentsAsync(string language, string badCode, float threshold, int limit)
        {
            var embeddings = await GetEmbeddingsAsync(badCode);
            var documents = await _copilotCommentsRepository.SearchLanguage(language);

            var searchResults = new List<CopilotSearchModel>();
            foreach (var document in documents)
            {
                var similarity = CosineSimilarity(document.Embedding, embeddings);
                if (similarity < threshold)
                    continue;
                searchResults.Add(new CopilotSearchModel(similarity, document));
            }

            var topResults = searchResults.OrderByDescending(item => item.similarity).Take(limit);
            return JsonSerializer.Serialize(topResults);
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
            var options = new EmbeddingsOptions(badCode);
            var response = await _openAIClient.GetEmbeddingsAsync(_configuration["OpenAI:Model"], options);
            return response.Value.Data[0].Embedding.ToArray();
        }

        private string GetDocumentJsonWithoutEmbedding(CopilotCommentModel document)
        {
            document.Embedding = null;
            return JsonSerializer.Serialize(document);
        }
        
        private IEnumerable<string> SplitGuidelineIds(string guidelineIds)
        {
            if (guidelineIds != null)
            {
                return guidelineIds.Split(',').Select(id => id.Trim());
            }
            return new List<string>();
        }
    }
}
