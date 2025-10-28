using APIViewWeb.LeanModels;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIViewWeb.Managers
{
    public interface INotificationManager
    {
        public Task NotifySubscribersOnComment(ClaimsPrincipal user, CommentItemModel comment);
        public Task NotifyUserOnCommentTag(CommentItemModel comment);
        public Task NotifyApproversOfReview(ClaimsPrincipal user, string apiRevisionId, HashSet<string> reviewers);
        public Task NotifyApproversOnNamespaceReviewRequest(ClaimsPrincipal user, ReviewListItemModel review, IEnumerable<ReviewListItemModel> languageReviews = null, string notes = "");
        public Task NotifyStakeholdersOfManualApproval(ReviewListItemModel review, IEnumerable<ReviewListItemModel> associatedReviews);
        public Task NotifyStakeholdersOfAutoApproval(ReviewListItemModel review, IEnumerable<ReviewListItemModel> associatedReviews);
        public Task NotifySubscribersOnNewRevisionAsync(ReviewListItemModel review, APIRevisionListItemModel revision, ClaimsPrincipal user);
        public Task ToggleSubscribedAsync(ClaimsPrincipal user, string reviewId, bool? state = null);
        public Task SubscribeAsync(ReviewListItemModel review, ClaimsPrincipal user);
        public Task UnsubscribeAsync(ReviewListItemModel review, ClaimsPrincipal user);
    }
}
