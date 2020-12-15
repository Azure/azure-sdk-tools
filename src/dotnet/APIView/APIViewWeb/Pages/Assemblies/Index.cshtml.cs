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

        [BindProperty(SupportsGet = true)]
        public bool Automatic { get; set; }

        public IEnumerable<ReviewModel> Assemblies { get; set; }

        public async Task OnGetAsync()
        {
            Assemblies = await _manager.GetReviewsAsync(Closed, Language, Automatic);
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

        public Dictionary<string, string> GetRoutingData(string language = null, bool? closed = null, bool? automatic = null)
        {
            var routingData = new Dictionary<string, string>();
            routingData["language"] = language ?? Language;
            routingData["closed"] = (closed ?? Closed) == true ? "true" : "false";
            routingData["automatic"] = (automatic ?? Automatic) == true ? "true" : "false";
            return routingData;
        }
    }
}
