using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Respositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class IndexPageModel : PageModel
    {
        private readonly ReviewManager _manager;

        public IndexPageModel(ReviewManager manager)
        {
            _manager = manager;
        }

        [FromForm]
        public UploadModel Upload { get; set; }

        [FromForm]
        public string Label { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool Closed { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Language { get; set; } = "All";

        public IEnumerable<ReviewModel> Assemblies { get; set; }

        public async Task OnGetAsync()
        {
            Assemblies = await _manager.GetReviewsAsync(Closed, Language);
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage();
            }

            var file = Upload.Files.SingleOrDefault();

            if (file != null)
            {
                using (var openReadStream = file.OpenReadStream())
                {
                    var reviewModel = await _manager.CreateReviewAsync(User, file.FileName, Label, openReadStream, Upload.RunAnalysis);
                    return RedirectToPage("Review", new { id = reviewModel.ReviewId });
                }
            }

            return RedirectToPage();
        }
    }
}
