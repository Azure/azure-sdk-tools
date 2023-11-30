using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Managers
{
    public interface IReviewManager
    {
        public Task<IEnumerable<ReviewListItemModel>> GetReviewsAsync(string language, bool? isClosed = false);
        public Task<ReviewListItemModel> GetReviewAsync(string language, string packageName, bool? isClosed = false);
        public Task<IEnumerable<ReviewListItemModel>> GetReviewsAssignedToUser(string userName);
        public Task<(IEnumerable<ReviewListItemModel> Reviews, int TotalCount, int TotalPages, int CurrentPage, int? PreviousPage, int? NextPage)> GetPagedReviewListAsync(
            IEnumerable<string> search, IEnumerable<string> languages, bool? isClosed, bool? isApproved, int offset, int limit, string orderBy);
        public Task<ReviewListItemModel> GetReviewAsync(ClaimsPrincipal user, string id);
        public Task<LegacyReviewModel> GetLegacyReviewAsync(ClaimsPrincipal user, string id);
        public Task<ReviewListItemModel> CreateReviewAsync(string packageName, string language, bool isClosed = true);
        public Task SoftDeleteReviewAsync(ClaimsPrincipal user, string id);
        public Task ToggleReviewIsClosedAsync(ClaimsPrincipal user, string id);
        public Task ToggleReviewApprovalAsync(ClaimsPrincipal user, string id, string revisionId, string notes="");
        public Task ApproveReviewAsync(ClaimsPrincipal user, string reviewId, string notes = "");
        public Task AssignReviewersToReviewAsync(ClaimsPrincipal User, string reviewId, HashSet<string> reviewers);
        public Task<int> GenerateAIReview(string reviewId, string revisionId);
        public Task UpdateReviewsInBackground(HashSet<string> updateDisabledLanguages, int backgroundBatchProcessCount);
    }
}
