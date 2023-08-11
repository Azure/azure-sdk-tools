using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.Azure.Cosmos;

namespace APIViewWeb.Repositories
{
    public interface ICopilotCommentsRepository
    {
        public Task InsertDocumentAsync(CopilotCommentModel document);

        public Task<CopilotCommentModel> UpdateDocumentAsync(string id, string language, IEnumerable<PatchOperation> updates);

        public Task DeleteDocumentAsync(string id, string language, IEnumerable<PatchOperation> updates);

        public Task<CopilotCommentModel> GetDocumentAsync(string id, string language);
        public IEnumerable<CopilotCommentModel> SearchLanguage(string language);
    }
}
