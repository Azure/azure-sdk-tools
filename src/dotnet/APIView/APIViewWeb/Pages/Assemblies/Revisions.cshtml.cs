using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class RevisionsPageModel : PageModel
    {
        private readonly IReviewManager _manager;
        public readonly UserPreferenceCache _preferenceCache;

        public RevisionsPageModel(
            IReviewManager manager, UserPreferenceCache preferenceCache)
        {
            _manager = manager;
            _preferenceCache = preferenceCache;
        }

        public ReviewModel Review { get; set; }

        [FromForm]
        public string Label { get; set; }

        [FromForm]
        public string FilePath { get; set; }

        [FromForm]
        public string Language { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            TempData["Page"] = "revisions";

            Review = await _manager.GetReviewAsync(User, id);

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
                await _manager.AddRevisionAsync(User, id, upload.FileName, Label, openReadStream, language: Language);
            }
            else
            {
                await _manager.AddRevisionAsync(User, id, FilePath, Label, null);
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id, string revisionId)
        {
            await _manager.DeleteRevisionAsync(User, id, revisionId);

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRenameAsync(string id, string revisionId, string newLabel)
        {
            await _manager.UpdateRevisionLabelAsync(User, id, revisionId, newLabel);

            return RedirectToPage();
        }
    }
}
