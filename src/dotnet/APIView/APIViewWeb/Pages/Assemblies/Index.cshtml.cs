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

        public ReviewsProperties ReviewsProperties { get; set; } = new ReviewsProperties();

        public async Task OnGetAsync()
        {
            _preferenceCache.UpdateUserPreference(new UserPreferenceModel() {
                UserName = User.GetGitHubLogin(),
                FilterType = this.FilterType,
                Language = this.Language,
            });

            ReviewsProperties.PackageNames = await _manager.GetReviewProprtiesAsync("PackageDisplayName");
            ReviewsProperties.ServiceNames = await _manager.GetReviewProprtiesAsync("ServiceName");
            ReviewsProperties.Authors = await _manager.GetReviewProprtiesAsync("Revisions[0].Author");
            Assemblies = await _manager.GetReviewsAsync(false, this.Language, null, this.FilterType);
        }

        public async Task<PartialViewResult> OnGetReviewsFilterPartialAsync()
        {
            ReviewsProperties.PackageNames = await _manager.GetReviewProprtiesAsync("PackageDisplayName");
            ReviewsProperties.ServiceNames = await _manager.GetReviewProprtiesAsync("ServiceName");
            return Partial("_ReviewsFiltersPartial", ReviewsProperties);
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

    public class ReviewsProperties 
    {
        public IEnumerable<string> PackageNames { get; set; } = new List<string>();
        public IEnumerable<string> ServiceNames { get; set; } = new List<string>();
        public IEnumerable<string> Authors { get; set; } = new List<string>();
    }
}
