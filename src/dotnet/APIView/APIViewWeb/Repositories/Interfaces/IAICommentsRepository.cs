using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;

namespace APIViewWeb.Repositories
{
    public interface IAICommentsRepository
    {
        public Task UpsertAICommentAsync(AICommentModel document);
        public Task DeleteAICommentAsync(string id, string user);
        public Task<AICommentModel> GetAICommentAsync(string id);
        public Task<IEnumerable<AICommentModelForSearch>> SimilaritySearchAsync(AICommentDTOForSearch aiCommentDTOForSearch, float[] embedding);
    }
}
