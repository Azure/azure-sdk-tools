using System.Collections.Generic;
using System.Linq;
using System.Web;
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

        public (IEnumerable<ReviewModel> Reviews,
            int TotalCount, int TotalPages, 
            int CurrentPage, int? PreviousPage, int? NextPage) PagedResults { get; set; }

        public async Task OnGetAsync()
        {
            _preferenceCache.UpdateUserPreference(new UserPreferenceModel() {
                UserName = User.GetGitHubLogin(),
                FilterType = this.FilterType,
                Language = this.Language,
            });

            ReviewsProperties.PackageNames = await _manager.GetReviewProprtiesAsync("PackageDisplayName");
            ReviewsProperties.Tags = await _manager.GetReviewProprtiesAsync("ServiceName");
            ReviewsProperties.Authors = await _manager.GetReviewProprtiesAsync("Author");
            ReviewsProperties.Languages = await _manager.GetReviewProprtiesAsync("Revisions[0].Files[0].Language");
            PagedResults = await _manager.GetPagedReviewsAsync(null, null, null, null, false, null, null);
        }

        public async Task<PartialViewResult> OnGetReviewsPartialAsync(
            List<string> packageNames=null, List<string> languages=null, List<string> authors=null,
            List<string> tags=null, bool? isClosed=false, bool isOpen=true,
            bool isManual=true, bool isAutomatic=true, bool isPullRequest=true, bool? isApproved=true,
            bool isPending=true, int offset=0, int limit=50)
        {
            packageNames = packageNames.Select(x => HttpUtility.UrlDecode(x)).ToList();
            languages = languages.Select(x => HttpUtility.UrlDecode(x)).ToList();
            authors = authors.Select(x => HttpUtility.UrlDecode(x)).ToList();
            tags = tags.Select(x => HttpUtility.UrlDecode(x)).ToList();

            // Resolve isClosed value
            if ((isOpen == true) && (isClosed == false))
            {
                isClosed = false;
            }
            else if ((isOpen == false) && (isClosed == true))
            {
                isClosed = true;
            }
            else
            {
                isClosed = null;
            }

            // Resolve FilterType
            List<int> filterTypes = new List<int>();
            if (isManual) { filterTypes.Add((int)ReviewType.Manual); } 
            if (isAutomatic) { filterTypes.Add((int)ReviewType.Automatic); }
            if (isPullRequest) { filterTypes.Add((int)ReviewType.PullRequest); }

            // Resolve Approval State
            if ((isApproved == true) && (isPending == false))
            {
                isApproved = true;
            }
            else if ((isApproved == false) && (isPending == true))
            {
                isApproved = false;
            }
            else
            {
                isApproved = null;
            }

            PagedResults = await _manager.GetPagedReviewsAsync(packageNames, languages, authors, tags, isClosed, filterTypes, isApproved, offset, limit);
            return Partial("_ReviewsPartial", PagedResults);
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
        public IEnumerable<string> Tags { get; set; } = new List<string>();
        public IEnumerable<string> Authors { get; set; } = new List<string>();
        public IEnumerable<string> Languages { get; set; } = new List<string>();
    }
}
