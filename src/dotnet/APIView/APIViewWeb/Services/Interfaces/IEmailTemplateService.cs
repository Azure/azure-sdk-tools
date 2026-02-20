using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;

namespace APIViewWeb.Services
{
    public interface IEmailTemplateService
    {
        Task<string> GetNamespaceReviewRequestEmailAsync(
            string packageName,
            string typeSpecUrl,
            IEnumerable<ReviewListItemModel> languageReviews,
            string notes = null);

        Task<string> GetNamespaceReviewApprovedEmailAsync(
            string packageName,
            string typeSpecUrl,
            IEnumerable<ReviewListItemModel> languageReviews);

        Task<string> GetApproverReviewEmailAsync(string requesterUserName, string reviewId, string reviewName);

        Task<string> GetCommentTagEmailAsync(CommentItemModel comment, ReviewListItemModel review, string reviewUrl);

        Task<string> GetSubscriberCommentEmailAsync(CommentItemModel comment, string elementUrl = null);

        Task<string> GetNewRevisionEmailAsync(ReviewListItemModel review, APIRevisionListItemModel revision);
    }
}
