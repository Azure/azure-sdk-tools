using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public interface ICosmosCommentsRepository
    {
        public Task<IEnumerable<CommentItemModel>> GetCommentsAsync(string reviewId);
        public Task UpsertCommentAsync(CommentItemModel commentModel);
        public Task<CommentItemModel> GetCommentAsync(string reviewId, string commentId);
        public Task<IEnumerable<CommentItemModel>> GetCommentsAsync(string reviewId, string lineId);
    }
}
