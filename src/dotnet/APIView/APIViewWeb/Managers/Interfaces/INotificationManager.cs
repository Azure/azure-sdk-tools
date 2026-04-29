using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIViewWeb.Managers.Interfaces
{
    public interface INotificationManager
    {
        public Task NotifySubscribersOnCommentAsync(ClaimsPrincipal user, CommentItemModel comment);
        public Task NotifyUserOnCommentTagAsync(CommentItemModel comment);
        public Task NotifyAssignedReviewersAsync(ClaimsPrincipal user, string apiRevisionId, HashSet<string> reviewers);
        public Task NotifyNamespaceReviewRequestRecipientsAsync(ClaimsPrincipal user, ReviewListItemModel review, IEnumerable<ReviewListItemModel> languageReviews = null, string notes = "");
        public Task NotifyStakeholdersOfManualApprovalAsync(ReviewListItemModel review, IEnumerable<ReviewListItemModel> associatedReviews);
        public Task NotifyNamespaceApprovedAsync(Project project, NamespaceDecisionEntry approvedEntry, ReviewListItemModel associatedReview);
        public Task NotifyNamespaceRejectedAsync(Project project, NamespaceDecisionEntry rejectedEntry, ReviewListItemModel associatedReview);
        public Task NotifySubscribersOnNewRevisionAsync(ReviewListItemModel review, APIRevisionListItemModel revision, ClaimsPrincipal user);
        public Task NotifySubscribersOnApprovalAsync(ReviewListItemModel review, APIRevisionListItemModel revision, ClaimsPrincipal user, bool isReviewApproval);
        public Task ToggleSubscribedAsync(ClaimsPrincipal user, string reviewId, bool? state = null);
        public Task SubscribeAsync(ReviewListItemModel review, ClaimsPrincipal user);
        public Task UnsubscribeAsync(ReviewListItemModel review, ClaimsPrincipal user);
    }
}
