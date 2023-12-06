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
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        public readonly UserPreferenceCache _preferenceCache;

        public IEnumerable<ReviewListItemModel> Reviews { get; set; } = new List<ReviewListItemModel>();
        public IEnumerable<APIRevisionListItemModel> ActiveAPIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> ApprovedAPIRevisions { get; set; } = new List<APIRevisionListItemModel>();

        public RequestedReviews(IReviewManager reviewManager, IAPIRevisionsManager apiRevisionsManager, UserPreferenceCache cache)
        {
            _reviewManager = reviewManager;
            _apiRevisionsManager = apiRevisionsManager;
            _preferenceCache = cache;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            Reviews = await _reviewManager.GetReviewsAssignedToUser(User.GetGitHubLogin());

            foreach (var review in Reviews.OrderByDescending(r => r.AssignedReviewers.Select(x => x.AssingedOn)))
            {
                var apiRevisoins = await _apiRevisionsManager.GetAPIRevisionsAsync(review.Id);
                ActiveAPIRevisions = ActiveAPIRevisions.Concat(apiRevisoins.Where(r => r.IsApproved == false));

                // Remove all approvals over a week old
                ApprovedAPIRevisions = ApprovedAPIRevisions.Concat(apiRevisoins.Where(r => r.IsApproved == true).Where(r => r.ChangeHistory.First(c => c.ChangeAction == APIRevisionChangeAction.Approved).ChangedOn >= DateTime.Now.AddDays(-7)));
            }
            ApprovedAPIRevisions.OrderByDescending(r => r.ChangeHistory.First(c => c.ChangeAction == APIRevisionChangeAction.Approved).ChangedOn);

            return Page();
        }
    }
}
