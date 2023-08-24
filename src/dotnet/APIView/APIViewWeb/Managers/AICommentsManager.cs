using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Linq;
using Azure;
using Azure.AI.OpenAI;
using APIViewWeb.Repositories;
using System.Text.Json;
using APIViewWeb.LeanModels;
 
namespace APIViewWeb.Managers
{
    public class AICommentsManager : IAICommentsManager
    {
        private readonly IAICommentsRepository _aiCommentsRepository;
        private readonly IConfiguration _configuration;
        private readonly OpenAIClient _openAIClient;

        public AICommentsManager(
            IAICommentsRepository aiCommentsRepository,
            IConfiguration configuration
            )
        {
            _aiCommentsRepository = aiCommentsRepository;
            _configuration = configuration;
            _openAIClient = new OpenAIClient(
                new Uri(_configuration["OpenAI:Endpoint"]),
                new AzureKeyCredential(_configuration["OpenAI:Key"]));
        }

        public async Task<AICommentModel> CreateAICommentAsync(AICommentDTO aiCommentDto, string user)
        {
            var commentModel = new AICommentModel()
            {
                Language = aiCommentDto.Language,
                BadCode = aiCommentDto.BadCode,
                GoodCode = aiCommentDto.GoodCode,
                Embedding = await GetEmbeddingsAsync(aiCommentDto.BadCode),
                Comment = aiCommentDto.Comment,
                GuidelineIds = aiCommentDto.GuidelineIds,
                ModifiedOn = DateTime.UtcNow,
                ModifiedBy = user
            };

            await _aiCommentsRepository.UpsertAICommentAsync(commentModel);
            commentModel.Embedding = null;
            return commentModel;
        }

        public async Task<AICommentModel> UpdateAICommentAsync(string id, AICommentDTO aiCommentDto, string user)
        {
            var commentModel = await _aiCommentsRepository.GetAICommentAsync(id);

            commentModel.ModifiedOn = DateTime.UtcNow;
            commentModel.ModifiedBy = user;

            if (aiCommentDto.Language != null)
            {
                commentModel.Language = aiCommentDto.Language;
            }

            if (aiCommentDto.BadCode != null)
            {
                var embedding = await GetEmbeddingsAsync(aiCommentDto.BadCode);
                commentModel.BadCode = aiCommentDto.BadCode;
                commentModel.Embedding = embedding;
            }

            if (aiCommentDto.GoodCode != null)
            {
                commentModel.GoodCode = aiCommentDto.GoodCode;
            }

            if (aiCommentDto.Comment != null)
            {
                commentModel.Comment = aiCommentDto.Comment;
            }

            if (aiCommentDto.GuidelineIds != null && aiCommentDto.GuidelineIds.Any())
            {
                commentModel.GuidelineIds = aiCommentDto.GuidelineIds;
            }

            await _aiCommentsRepository.UpsertAICommentAsync(commentModel);
            commentModel.Embedding = null;
            return commentModel;
        }

        public async Task DeleteAICommentAsync(string id, string user)
        {
            await _aiCommentsRepository.DeleteAICommentAsync(id, user); 
        }

        public async Task<AICommentModel> GetAICommentAsync(string id)
        {
            var commentModel = await _aiCommentsRepository.GetAICommentAsync(id);
            commentModel.Embedding = null;
            return commentModel;
        }

        public async Task<IEnumerable<AICommentModelForSearch>> SearchAICommentAsync(AICommentDTOForSearch aiCommentDTOForSearch)
        {
            var embeddings = (aiCommentDTOForSearch.Embedding == null || !aiCommentDTOForSearch.Embedding.Any()) ?
                await GetEmbeddingsAsync(aiCommentDTOForSearch.BadCode): aiCommentDTOForSearch.Embedding;
            
            var searchResults = await _aiCommentsRepository.SimilaritySearchAsync(aiCommentDTOForSearch);
            var topResults = searchResults.Where(x => x.Similarity >= aiCommentDTOForSearch.Threshold);
            return topResults;
        }

        private async Task<float[]> GetEmbeddingsAsync(string badCode)
        {
            var options = new EmbeddingsOptions(badCode);
            var response = await _openAIClient.GetEmbeddingsAsync(_configuration["OpenAI:Model"], options);
            return response.Value.Data[0].Embedding.ToArray();
        }
    }
}
