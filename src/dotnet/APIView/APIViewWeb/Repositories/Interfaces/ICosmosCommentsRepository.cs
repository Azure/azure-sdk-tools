using APIViewWeb.LeanModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public interface ICosmosCommentsRepository
    {
        public Task<IEnumerable<CommentItemModel>> GetCommentsAsync(string reviewId, bool isDeleted = false, CommentType? commentType = null);
        public Task UpsertCommentAsync(CommentItemModel commentModel);
        public Task<CommentItemModel> GetCommentAsync(string reviewId, string commentId);
        public Task<IEnumerable<CommentItemModel>> GetCommentsAsync(string reviewId, string lineId);
    }
}
