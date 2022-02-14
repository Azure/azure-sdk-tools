using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class IndexPageModel : PageModel
    {
        private readonly ReviewManager _manager;
        private readonly UserPreferenceCache _preferenceCache;

        public IndexPageModel(ReviewManager manager, UserPreferenceCache preferenceCache)
        {
            _manager = manager;
            _preferenceCache = preferenceCache;
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
        public ReviewType FilterType { get; set; } = ReviewType.Automatic;

        public IEnumerable<ReviewModel> Assemblies { get; set; } = new List<ReviewModel>();

        public IEnumerable<ServiceGroupModel> reviewServices { get; set; }

        public async Task OnGetAsync()
        {
            _preferenceCache.UpdateUserPreference(new UserPreferenceModel() {
                UserName = User.GetGitHubLogin(),
                FilterType = this.FilterType,
                Language = this.Language,
            });

            reviewServices = await _manager.GetReviewsByServicesAsync(FilterType);
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

        public Dictionary<string, string> GetRoutingData(string language = null, bool? closed = null, ReviewType filterType = ReviewType.Manual)
        {
            var routingData = new Dictionary<string, string>();
            routingData["language"] = language ?? Language;
            routingData["closed"] = (closed ?? Closed) == true ? "true" : "false";
            routingData["filterType"] = filterType.ToString();
            return routingData;
        }
    }
}
