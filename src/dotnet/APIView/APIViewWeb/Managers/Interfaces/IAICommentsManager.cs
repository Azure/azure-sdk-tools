using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Managers
{
    public interface IAICommentsManager
    {
        public Task<AICommentModel> CreateAICommentAsync(AICommentDTO aiCommentDto, string user);
        public Task<AICommentModel> UpdateAICommentAsync(string id, AICommentDTO aiCommentDto, string user);
        public Task<AICommentModel> GetAICommentAsync(string id);
        public Task DeleteAICommentAsync(string id, string user);
        public Task<IEnumerable<AICommentModelForSearch>> SearchAICommentAsync(AICommentDTOForSearch aiCommentDTOForSearch);
    }
}
