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
        public readonly UserProfileCache _userProfileCache;

        public DeleteModel(IReviewManager manager, UserProfileCache userProfileCache)
        {
            _manager = manager;
            _userProfileCache = userProfileCache;
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
