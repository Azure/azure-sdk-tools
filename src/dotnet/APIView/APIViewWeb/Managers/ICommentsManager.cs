using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Models;

namespace APIViewWeb.Managers
{
    public interface ICommentsManager
    {
        public void LoadTaggableUsers();
        public Task<ReviewCommentsModel> GetReviewCommentsAsync(string reviewId);
        public Task<ReviewCommentsModel> GetUsageSampleCommentsAsync(string reviewId);
        public Task AddCommentAsync(ClaimsPrincipal user, CommentModel comment);
        public Task<CommentModel> UpdateCommentAsync(ClaimsPrincipal user, string reviewId, string commentId, string commentText, string[] taggedUsers);
        public Task DeleteCommentAsync(ClaimsPrincipal user, string reviewId, string commentId);
        public Task ResolveConversation(ClaimsPrincipal user, string reviewId, string lineId);
        public Task UnresolveConversation(ClaimsPrincipal user, string reviewId, string lineId);
        public Task ToggleUpvoteAsync(ClaimsPrincipal user, string reviewId, string commentId);
        public HashSet<GithubUser> GetTaggableUsers();
    }
}
