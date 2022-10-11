using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
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
        private readonly ReviewManager _manager;
        public readonly UserPreferenceCache _preferenceCache;
        public IEnumerable<ReviewModel> ActiveReviews { get; set; } = new List<ReviewModel>();
        public IEnumerable<ReviewModel> ApprovedReviews { get; set; } = new List<ReviewModel>();

        public RequestedReviews(ReviewManager manager, UserPreferenceCache cache)
        {
            _manager = manager;
            _preferenceCache = cache;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var fullResult = (await _manager.GetReviewsAsync(false, "All")).Where(r => r.RequestedReviewers != null).Where(r => r.RequestedReviewers.Contains(User.GetGitHubLogin()));
            ActiveReviews = fullResult.Where(r => r.IsApproved == false).OrderByDescending(r => r.ApprovalRequestedOn);
            // Remove all approvals over a week old
            ApprovedReviews = fullResult.Where(r => r.IsApproved == true).Where(r => r.ApprovalDate != null).Where(r => r.ApprovalDate >= DateTime.Now.AddDays(-7)).OrderByDescending(r => r.ApprovalDate); 
            return Page();
        }
    }
}
