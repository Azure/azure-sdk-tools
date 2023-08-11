using System.Threading.Tasks;
using APIViewWeb.Models;
using MongoDB.Driver;

namespace APIViewWeb.Repositories
{
    public interface ICopilotCommentsRepository
    {
        public Task<string> InsertDocumentAsync(CopilotCommentModel document);

        public Task<UpdateResult> UpdateDocumentAsync(
            FilterDefinition<CopilotCommentModel> filter,
            UpdateDefinition<CopilotCommentModel> update);

        public Task DeleteDocumentAsync(
            FilterDefinition<CopilotCommentModel> filter,
            UpdateDefinition<CopilotCommentModel> update);

        public Task<CopilotCommentModel> GetDocumentAsync(FilterDefinition<CopilotCommentModel> filter);
    }
}
