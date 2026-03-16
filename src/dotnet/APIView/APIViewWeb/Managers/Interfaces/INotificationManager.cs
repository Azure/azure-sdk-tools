using APIViewWeb.LeanModels;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIViewWeb.Managers
{
    public interface INotificationManager
    {
        public Task NotifySubscribersOnCommentAsync(ClaimsPrincipal user, CommentItemModel comment);
        public Task NotifyUserOnCommentTagAsync(CommentItemModel comment);
        public Task NotifyAssignedReviewersAsync(ClaimsPrincipal user, string apiRevisionId, HashSet<string> reviewers);
        public Task NotifyNamespaceReviewRequestRecipientsAsync(ClaimsPrincipal user, ReviewListItemModel review, IEnumerable<ReviewListItemModel> languageReviews = null, string notes = "");
        public Task NotifyStakeholdersOfManualApprovalAsync(ReviewListItemModel review, IEnumerable<ReviewListItemModel> associatedReviews);
        public Task NotifySubscribersOnNewRevisionAsync(ReviewListItemModel review, APIRevisionListItemModel revision, ClaimsPrincipal user);
        public Task ToggleSubscribedAsync(ClaimsPrincipal user, string reviewId, bool? state = null);
        public Task SubscribeAsync(ReviewListItemModel review, ClaimsPrincipal user);
        public Task UnsubscribeAsync(ReviewListItemModel review, ClaimsPrincipal user);
    }
}
