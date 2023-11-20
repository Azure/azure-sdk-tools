using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class RevisionsPageModel : PageModel
    {
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        public readonly UserPreferenceCache _preferenceCache;

        public RevisionsPageModel(
            IReviewManager manager,
            IAPIRevisionsManager reviewRevisionsManager,
            UserPreferenceCache preferenceCache)
        {
            _reviewManager = manager;
            _apiRevisionsManager = reviewRevisionsManager;
            _preferenceCache = preferenceCache;
        }

        public ReviewListItemModel Review { get; set; }
        public APIRevisionListItemModel LatestAPIRevision { get; set; }
        public Dictionary<string, List<APIRevisionListItemModel>> APIRevisions { get; set; }

        [FromForm]
        public string Label { get; set; }

        [FromForm]
        public string FilePath { get; set; }

        [FromForm]
        public string Language { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            TempData["Page"] = "revisions";

            Review = await _reviewManager.GetReviewAsync(User, id);
            LatestAPIRevision = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(Review.Id);
            var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(Review.Id);
            APIRevisions = revisions.GroupBy(r => r.APIRevisionType).ToDictionary(r => r.Key.ToString(), r => r.ToList());

            return Page();
        }

        public async Task<IActionResult> OnPostUploadAsync(string id, [FromForm] IFormFile upload)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage();
            }

            if (upload != null)
            {
                var openReadStream = upload.OpenReadStream();
                await _apiRevisionsManager.AddAPIRevisionAsync(User, id, APIRevisionType.Manual, upload.FileName, Label, openReadStream, language: Language);
            }
            else
            {
                await _apiRevisionsManager.AddAPIRevisionAsync(User, id, APIRevisionType.Manual, FilePath, Label, null);
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id, string revisionId)
        {
            await _apiRevisionsManager.SoftDeleteAPIRevisionAsync(User, id, revisionId);

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRenameAsync(string id, string revisionId, string newLabel)
        {
            await _apiRevisionsManager.UpdateAPIRevisionLabelAsync(User, revisionId, newLabel);

            return RedirectToPage();
        }
    }
}
