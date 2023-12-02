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
                ChangeHistory = new List<AICommentChangeHistoryModel>()
                {
                    new AICommentChangeHistoryModel()
                    {
                        ChangeAction = AICommentChangeAction.Created,
                        ChangedBy = user,
                        ChangedOn = DateTime.UtcNow
                    }
                }
            };

            await _aiCommentsRepository.UpsertAICommentAsync(commentModel);
            commentModel.Embedding = null;
            return commentModel;
        }

        public async Task<AICommentModel> UpdateAICommentAsync(string id, AICommentDTO aiCommentDto, string user)
        {
            var aiCommentModel = await _aiCommentsRepository.GetAICommentAsync(id);

            if (aiCommentDto.Language != null)
            {
                aiCommentModel.Language = aiCommentDto.Language;
            }

            if (aiCommentDto.BadCode != null)
            {
                var embedding = await GetEmbeddingsAsync(aiCommentDto.BadCode);
                aiCommentModel.BadCode = aiCommentDto.BadCode;
                aiCommentModel.Embedding = embedding;
            }

            if (aiCommentDto.GoodCode != null)
            {
                aiCommentModel.GoodCode = aiCommentDto.GoodCode;
            }

            if (aiCommentDto.Comment != null)
            {
                aiCommentModel.Comment = aiCommentDto.Comment;
            }

            if (aiCommentDto.GuidelineIds != null && aiCommentDto.GuidelineIds.Any())
            {
                aiCommentModel.GuidelineIds = aiCommentDto.GuidelineIds;
            }

            aiCommentModel.ChangeHistory.Add(new AICommentChangeHistoryModel()
            {
                ChangeAction = AICommentChangeAction.Modified,
                ChangedBy = user,
                ChangedOn = DateTime.UtcNow
            });

            await _aiCommentsRepository.UpsertAICommentAsync(aiCommentModel);
            aiCommentModel.Embedding = null;
            return aiCommentModel;
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
            var embedding = await GetEmbeddingsAsync(aiCommentDTOForSearch.BadCode);
            var searchResults = await _aiCommentsRepository.SimilaritySearchAsync(aiCommentDTOForSearch, embedding);
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
