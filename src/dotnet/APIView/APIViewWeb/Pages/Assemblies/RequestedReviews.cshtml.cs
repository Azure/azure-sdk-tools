using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.VisualStudio.Services.Common;
using Octokit;

namespace APIViewWeb.Pages.Assemblies
{
    public class RequestedReviews: PageModel
    {
        private readonly IReviewManager _manager;
        public readonly UserPreferenceCache _preferenceCache;
        public IEnumerable<ReviewListItemModel> ActiveReviews { get; set; } = new List<ReviewListItemModel>();
        public IEnumerable<ReviewListItemModel> ApprovedReviews { get; set; } = new List<ReviewListItemModel>();

        public RequestedReviews(IReviewManager manager, UserPreferenceCache cache)
        {
            _manager = manager;
            _preferenceCache = cache;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var requestedReviews = await _manager.GetReviewsAssignedToUser(User.GetGitHubLogin());
            ActiveReviews = requestedReviews.Where(r => r.IsApproved == false).OrderByDescending(r => r.AssignedReviewers.Select(x => x.AssingedOn));
            // Remove all approvals over a week old
            //ApprovedReviews = requestedReviews.Where(r => r.IsApproved == true).Where(r => r.ApprovalDate >= DateTime.Now.AddDays(-7)).OrderByDescending(r => r.ApprovalDate);
            return Page();
        }
    }
}
