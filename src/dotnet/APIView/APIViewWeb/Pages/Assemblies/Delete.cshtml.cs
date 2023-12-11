using System.Threading.Tasks;
using APIViewWeb.Managers;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class DeleteModel : PageModel
    {
        private readonly IReviewManager _manager;
        public readonly UserPreferenceCache _preferenceCache;

        public DeleteModel(IReviewManager manager, UserPreferenceCache preferenceCache)
        {
            _manager = manager;
            _preferenceCache = preferenceCache;
        }

        public string AssemblyName { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reviewModel = await _manager.GetReviewAsync(User, id);
            AssemblyName = reviewModel.PackageName;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            await _manager.SoftDeleteReviewAsync(User, id);

            return RedirectToPage("./Index");
        }
    }
}
