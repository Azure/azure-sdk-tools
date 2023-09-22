using APIViewWeb.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public interface ICosmosCommentsRepository
    {
        public Task<IEnumerable<CommentModel>> GetCommentsAsync(string reviewId);
        public Task UpsertCommentAsync(CommentModel commentModel);
        public Task DeleteCommentAsync(CommentModel commentModel);
        public Task DeleteCommentsAsync(string reviewId);
        public Task<CommentModel> GetCommentAsync(string reviewId, string commentId);
        public Task<IEnumerable<CommentModel>> GetCommentsAsync(string reviewId, string lineId);
    }
}
