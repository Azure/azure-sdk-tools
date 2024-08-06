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

        public IEnumerable<APIRevisionListItemModel> APIRevisions { get; set; } = new List<APIRevisionListItemModel>();
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
            var userId = User.GetGitHubLogin();
            APIRevisions = await _apiRevisionsManager.GetAPIRevisionsAssignedToUser(userId);

            List<APIRevisionListItemModel> activeAPIRevs = new List<APIRevisionListItemModel>();
            List<APIRevisionListItemModel> approvedAPIRevs = new List<APIRevisionListItemModel>();
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
            }
            ActiveAPIRevisions = activeAPIRevs;
            ApprovedAPIRevisions = approvedAPIRevs;
            ApprovedAPIRevisions.OrderByDescending(r => r.ChangeHistory.First(c => c.ChangeAction == APIRevisionChangeAction.Approved).ChangedOn);

            return Page();
        }
    }
}
