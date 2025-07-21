using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{    
    public class RequestedReviews: PageModel
    {
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IReviewManager _reviewManager;
        public readonly UserProfileCache _userProfileCache;
        public IEnumerable<APIRevisionListItemModel> APIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> ActiveAPIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> ApprovedAPIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> NamespaceApprovalRequestedAPIRevisions { get; set; } = new List<APIRevisionListItemModel>();        
        public RequestedReviews(IAPIRevisionsManager apiRevisionsManager, IReviewManager reviewManager, UserProfileCache userProfileCache)
        {
            _apiRevisionsManager = apiRevisionsManager;
            _reviewManager = reviewManager;
            _userProfileCache = userProfileCache;
        }        
        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.GetGitHubLogin();
            APIRevisions = await _apiRevisionsManager.GetAPIRevisionsAssignedToUser(userId);

            List<APIRevisionListItemModel> activeAPIRevs = new List<APIRevisionListItemModel>();
            List<APIRevisionListItemModel> approvedAPIRevs = new List<APIRevisionListItemModel>();
            List<APIRevisionListItemModel> namespaceApprovalRequestedAPIRevs = new List<APIRevisionListItemModel>();

            // Get all unique review IDs to minimize database calls
            var reviewIds = APIRevisions.Select(r => r.ReviewId).Distinct().ToList();
            var reviews = new Dictionary<string, ReviewListItemModel>();
            
            // Fetch all parent reviews in batch using parallel processing
            var reviewTasks = reviewIds.Select(async reviewId =>
            {
                var review = await _reviewManager.GetReviewAsync(User, reviewId);
                return (reviewId, review);
            });

            var reviewResults = await Task.WhenAll(reviewTasks);
            
            foreach (var (reviewId, review) in reviewResults)
            {
                if (review != null)
                {
                    reviews[reviewId] = review;
                }
            }

            foreach (var apiRevision in APIRevisions.OrderByDescending(r => r.AssignedReviewers.First(x => x.AssingedTo.Equals(userId)).AssingedOn))
            {
                if (!apiRevision.IsApproved)
                {
                    activeAPIRevs.Add(apiRevision);
                }

                if (apiRevision.IsApproved && apiRevision.ChangeHistory.First(c => c.ChangeAction == APIRevisionChangeAction.Approved).ChangedOn >= DateTime.Now.AddDays(-7))
                {
                    approvedAPIRevs.Add(apiRevision);
                }

                // Check if the parent review has namespace approval requested using cached data
                if (reviews.TryGetValue(apiRevision.ReviewId, out var parentReview) && 
                    parentReview.IsNamespaceReviewRequested && 
                    !parentReview.IsApproved)
                {
                    namespaceApprovalRequestedAPIRevs.Add(apiRevision);
                }
            }
            
            ActiveAPIRevisions = activeAPIRevs;
            ApprovedAPIRevisions = approvedAPIRevs;
            NamespaceApprovalRequestedAPIRevisions = namespaceApprovalRequestedAPIRevs;
            ApprovedAPIRevisions.OrderByDescending(r => r.ChangeHistory.First(c => c.ChangeAction == APIRevisionChangeAction.Approved).ChangedOn);

            return Page();
        }
    }
}
