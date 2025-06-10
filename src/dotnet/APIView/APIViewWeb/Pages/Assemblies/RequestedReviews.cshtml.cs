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
{    public class RequestedReviews: PageModel
    {
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IReviewManager _reviewManager;
        public readonly UserProfileCache _userProfileCache;public IEnumerable<APIRevisionListItemModel> APIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> ActiveAPIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> ApprovedAPIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> NamespaceApprovalRequestedAPIRevisions { get; set; } = new List<APIRevisionListItemModel>();        public RequestedReviews(IAPIRevisionsManager apiRevisionsManager, IReviewManager reviewManager, UserProfileCache userProfileCache)
        {
            _apiRevisionsManager = apiRevisionsManager;
            _reviewManager = reviewManager;
            _userProfileCache = userProfileCache;
        }        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.GetGitHubLogin();
            APIRevisions = await _apiRevisionsManager.GetAPIRevisionsAssignedToUser(userId);

            List<APIRevisionListItemModel> activeAPIRevs = new List<APIRevisionListItemModel>();
            List<APIRevisionListItemModel> approvedAPIRevs = new List<APIRevisionListItemModel>();
            List<APIRevisionListItemModel> namespaceApprovalRequestedAPIRevs = new List<APIRevisionListItemModel>();

            // Get all unique review IDs to minimize database calls
            var reviewIds = APIRevisions.Select(r => r.ReviewId).Distinct().ToList();
            var reviews = new Dictionary<string, ReviewListItemModel>();
            
            // Fetch all parent reviews in batch
            foreach (var reviewId in reviewIds)
            {
                var review = await _reviewManager.GetReviewAsync(User, reviewId);
                if (review != null)
                {
                    reviews[reviewId] = review;
                }
            }

            foreach (var apiRevison in APIRevisions.OrderByDescending(r => r.AssignedReviewers.First(x => x.AssingedTo.Equals(userId)).AssingedOn))
            {
                if (!apiRevison.IsApproved)
                {
                    activeAPIRevs.Add(apiRevison);
                }

                if (apiRevison.IsApproved && apiRevison.ChangeHistory.First(c => c.ChangeAction == APIRevisionChangeAction.Approved).ChangedOn >= DateTime.Now.AddDays(-7))
                {
                    approvedAPIRevs.Add(apiRevison);
                }

                // Check if the parent review has namespace approval requested using cached data
                if (reviews.TryGetValue(apiRevison.ReviewId, out var parentReview) && 
                    parentReview.IsNamespaceReviewRequested && 
                    !parentReview.IsNamespaceApproved)
                {
                    namespaceApprovalRequestedAPIRevs.Add(apiRevison);
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
