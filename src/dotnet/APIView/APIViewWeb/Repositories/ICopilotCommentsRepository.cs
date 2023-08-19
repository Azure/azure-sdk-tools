using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Models;

namespace APIViewWeb.Repositories
{
    public interface ICopilotCommentsRepository
    {
        public Task InsertDocumentAsync(CopilotCommentModel document);

        public Task UpdateDocumentAsync(CopilotCommentModel document);

        public Task DeleteDocumentAsync(string id, string language, string user);

        public Task<CopilotCommentModel> GetDocumentAsync(string id, string language);
        public Task<IEnumerable<CopilotSearchModel>> SimilaritySearchAsync(string language, float[] embedding, float threshold, int limit);
    }
}
