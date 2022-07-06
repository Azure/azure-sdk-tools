using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Azure;

namespace APIViewWeb.Pages.Assemblies
{
    public class IndexPageModel : PageModel
    {
        private readonly ReviewManager _manager;
        private readonly UserPreferenceCache _preferenceCache;
        public const int _defaultPageSize = 50;
        public const string _defaultSortField = "LastUpdated";

        public IndexPageModel(ReviewManager manager, UserPreferenceCache preferenceCache)
        {
            _manager = manager;
            _preferenceCache = preferenceCache;
        }

        [FromForm]
        public UploadModel Upload { get; set; }

        [FromForm]
        public string Label { get; set; }

        public ReviewsProperties ReviewsProperties { get; set; } = new ReviewsProperties();

        public (IEnumerable<ReviewModel> Reviews, int TotalCount, int TotalPages,
            int CurrentPage, int? PreviousPage, int? NextPage) PagedResults { get; set; }

        public async Task OnGetAsync(
            List<string> search = null, List<string> languages=null, List<string> state =null,
            List<string> status =null, List<string> type =null, int pageNo=1, int pageSize=_defaultPageSize, string sortField=_defaultSortField)
        {
            await RunGetRequest(search, languages, state, status, type, pageNo, pageSize, sortField);
        }

        public async Task<PartialViewResult> OnGetReviewsPartialAsync(
            List<string> search = null, List<string> languages = null, List<string> state = null,
            List<string> status = null, List<string> type = null, int pageNo = 1, int pageSize=_defaultPageSize, string sortField=_defaultSortField)
        {
            await RunGetRequest(search, languages, state, status, type, pageNo, pageSize, sortField);
            return Partial("_ReviewsPartial", PagedResults);
        }

        public async Task<PartialViewResult> OnGetReviewsLanguagesAsync(List<string> selectedLanguages = null)
        {
            ReviewsProperties.Languages.All = await _manager.GetReviewPropertiesAsync("Revisions[0].Files[0].Language");
            selectedLanguages = selectedLanguages.Select(x => HttpUtility.UrlDecode(x)).ToList();
            ReviewsProperties.Languages.Selected = selectedLanguages;
            return Partial("_SelectPickerPartial", ReviewsProperties.Languages);
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

        private async Task RunGetRequest(List<string> search, List<string> languages,
            List<string> state, List<string> status, List<string> type, int pageNo, int pageSize, string sortField)
        {
            search = search.Select(x => HttpUtility.UrlDecode(x)).ToList();
            languages = languages.Select(x => HttpUtility.UrlDecode(x)).ToList();
            state = state.Select(x => HttpUtility.UrlDecode(x)).ToList();
            status = status.Select(x => HttpUtility.UrlDecode(x)).ToList();
            type = type.Select(x => HttpUtility.UrlDecode(x)).ToList();

            // Update selected properties
            if (state.Count() > 0)
            {
                ReviewsProperties.State.Selected = state;
            }
            else 
            {
                state = ReviewsProperties.State.Selected.ToList();
            }

            if (status.Count() > 0)
            {
                ReviewsProperties.Status.Selected = status;
            }

            if (type.Count() > 0)
            {
                ReviewsProperties.Type.Selected = type;
            }
            
            bool? isClosed = null;
            // Resolve isClosed value
            if (state.Contains("Open") && !state.Contains("Closed"))
            {
                isClosed = false;
            }
            else if (!state.Contains("Open") && state.Contains("Closed"))
            {
                isClosed = true;
            }
            else
            {
                isClosed = null;
            }

            // Resolve FilterType
            List<int> filterTypes = new List<int>();
            if (type.Contains("Manual")) { filterTypes.Add((int)ReviewType.Manual); }
            if (type.Contains("Automatic")) { filterTypes.Add((int)ReviewType.Automatic); }
            if (type.Contains("PullRequest")) { filterTypes.Add((int)ReviewType.PullRequest); }

            _preferenceCache.UpdateUserPreference(new UserPreferenceModel()
            {
                UserName = User.GetGitHubLogin(),
                FilterType = filterTypes.Cast<ReviewType>().ToList(),
                Language = languages
            });

            bool? isApproved = null;
            // Resolve Approval State
            if (status.Contains("Approved") && !status.Contains("Pending"))
            {
                isApproved = true;
            }
            else if (!status.Contains("Approved") && status.Contains("Pending"))
            {
                isApproved = false;
            }
            else
            {
                isApproved = null;
            }
            var offset = (pageNo - 1) * pageSize;

            PagedResults = await _manager.GetPagedReviewsAsync(search, languages, isClosed, filterTypes, isApproved, offset, pageSize, sortField);
        }
    }

    public class ReviewsProperties 
    {
        public (IEnumerable<string> All, IEnumerable<string> Selected) Languages = (All: new List<string>(), Selected: new List<string>());
        public (IEnumerable<string> All, IEnumerable<string> Selected) State = (All: new List<string> { "Closed", "Open" }, Selected: new List<string> { "Open" });
        public (IEnumerable<string> All, IEnumerable<string> Selected) Status = (All: new List<string> { "Approved", "Pending" }, Selected: new List<string>());
        public (IEnumerable<string> All, IEnumerable<string> Selected) Type = (All: new List<string> { "Automatic", "Manual", "PullRequest" }, Selected: new List<string>());
    }
}
