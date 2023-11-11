using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIViewWeb.Managers
{
    public interface INotificationManager
    {
        public Task NotifySubscribersOnComment(ClaimsPrincipal user, CommentItemModel comment);
        public Task NotifyUserOnCommentTag(CommentItemModel comment);
        public Task NotifyApproversOfReview(ClaimsPrincipal user, string reviewId, HashSet<string> reviewers);
        public Task NotifySubscribersOnNewRevisionAsync(ReviewListItemModel review, APIRevisionListItemModel revision, ClaimsPrincipal user);
        public Task ToggleSubscribedAsync(ClaimsPrincipal user, string reviewId);
        public Task SubscribeAsync(ReviewListItemModel review, ClaimsPrincipal user);
        public Task UnsubscribeAsync(ReviewListItemModel review, ClaimsPrincipal user);
    }
}
