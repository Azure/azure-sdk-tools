using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class LegacyReview: PageModel
    {
        private CommentsManager _commentsManager;

        public LegacyReview(CommentsManager commentsManager)
        {
            _commentsManager = commentsManager;
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
