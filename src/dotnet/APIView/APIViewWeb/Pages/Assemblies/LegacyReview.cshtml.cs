using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class LegacyReview: PageModel
    {
        private ICommentsManager _commentsManager;
        public readonly UserPreferenceCache _preferenceCache;

        public LegacyReview(ICommentsManager commentsManager, UserPreferenceCache preferenceCache)
        {
            _commentsManager = commentsManager;
            _preferenceCache = preferenceCache;
        }

        public string Id { get; set; }

        public ReviewCommentsModel Comments { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            Id = id;
            Comments = await _commentsManager.GetReviewCommentsAsync(id);
            return Page();
        }
    }
}
