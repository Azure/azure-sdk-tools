using System.Threading.Tasks;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class DeleteModel : PageModel
    {
        private readonly ReviewManager _manager;

        public DeleteModel(ReviewManager manager)
        {
            _manager = manager;
        }

        public string AssemblyName { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reviewModel = await _manager.GetReviewAsync(User, id);
            AssemblyName = reviewModel.DisplayName;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            await _manager.DeleteReviewAsync(User, id);

            return RedirectToPage("./Index");
        }
    }
}
