using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Respositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class FilesPageModel : PageModel
    {
        private readonly ReviewManager _manager;


        public FilesPageModel(
            ReviewManager manager)
        {
            _manager = manager;
        }

        public ReviewModel Review { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            TempData["Page"] = "files";

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
                await _manager.AddFileAsync(User, id, upload.FileName, openReadStream);
            }

            return RedirectToPage();
        }
    }
}
