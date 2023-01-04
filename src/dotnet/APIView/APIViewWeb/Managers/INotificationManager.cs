using APIViewWeb.Models;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIViewWeb.Managers
{
    public interface INotificationManager
    {
        public Task NotifySubscribersOnComment(ClaimsPrincipal user, CommentModel comment);
        public Task NotifyUserOnCommentTag(string username, CommentModel comment);
        public Task NotifyApproversOfReview(ClaimsPrincipal user, string reviewId, HashSet<string> reviewers);
        public Task NotifySubscribersOnNewRevisionAsync(ReviewRevisionModel revision, ClaimsPrincipal user);
        public Task ToggleSubscribedAsync(ClaimsPrincipal user, string reviewId);
        public Task SubscribeAsync(ReviewModel review, ClaimsPrincipal user);
        public Task UnsubscribeAsync(ReviewModel review, ClaimsPrincipal user);
    }
}
