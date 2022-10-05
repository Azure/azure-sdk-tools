using System;
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
        public readonly UserPreferenceCache _preferenceCache;
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
            IEnumerable<string> search, IEnumerable<string> languages, IEnumerable<string> state,
            IEnumerable<string> status, IEnumerable<string> type, int pageNo=1, int pageSize=_defaultPageSize, string sortField=_defaultSortField)
        {
            if (!search.Any() && !languages.Any() && !state.Any() && !status.Any() && !type.Any())
            {
                UserPreferenceModel userPreference = await _preferenceCache.GetUserPreferences(User.GetGitHubLogin());
                languages = userPreference.Language;
                state = userPreference.State;
                status = userPreference.Status;
                type = userPreference.FilterType.Select(x => x.ToString());
            }
            await RunGetRequest(search, languages, state, status, type, pageNo, pageSize, sortField);
        }

        public async Task<PartialViewResult> OnGetReviewsPartialAsync(
            IEnumerable<string> search, IEnumerable<string> languages, IEnumerable<string> state,
            IEnumerable<string> status, IEnumerable<string> type, int pageNo = 1, int pageSize=_defaultPageSize, string sortField=_defaultSortField)
        {
            await RunGetRequest(search, languages, state, status, type, pageNo, pageSize, sortField);
            return Partial("_ReviewsPartial", PagedResults);
        }

        public async Task<PartialViewResult> OnGetReviewsLanguagesAsync(IEnumerable<string> selectedLanguages)
        {
            if (!selectedLanguages.Any())
            {
                UserPreferenceModel userPreference = await _preferenceCache.GetUserPreferences(User.GetGitHubLogin());
                selectedLanguages = userPreference.Language.ToList();
            }
            ReviewsProperties.Languages.All = await _manager.GetReviewPropertiesAsync("Revisions[0].Files[0].Language");
            selectedLanguages = selectedLanguages.Select(x => HttpUtility.UrlDecode(x));
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

        private async Task RunGetRequest(IEnumerable<string> search, IEnumerable<string> languages,
            IEnumerable<string> state, IEnumerable<string> status, IEnumerable<string> type, int pageNo, int pageSize, string sortField)
        {
            search = search.Select(x => HttpUtility.UrlDecode(x));
            languages = languages.Select(x => HttpUtility.UrlDecode(x));
            state = state.Select(x => HttpUtility.UrlDecode(x));
            status = status.Select(x => HttpUtility.UrlDecode(x));
            type = type.Select(x => HttpUtility.UrlDecode(x));

            // Update selected properties
            if (state.Any())
            {
                ReviewsProperties.State.Selected = state;
            }
            else 
            {
                state = ReviewsProperties.State.Selected;
            }

            if (status.Any())
            {
                ReviewsProperties.Status.Selected = status;
            }

            if (type.Any())
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
            IEnumerable<ReviewType> filterTypes = type.Select(x => (ReviewType)Enum.Parse(typeof(ReviewType), x));
            IEnumerable<int> filterTypesAsInt = filterTypes.Select(x => (int)x);

            _preferenceCache.UpdateUserPreference(new UserPreferenceModel {
                FilterType = filterTypes,
                Language = languages,
                State = state,
                Status = status
            }, User.GetGitHubLogin());

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

            PagedResults = await _manager.GetPagedReviewsAsync(search, languages, isClosed, filterTypesAsInt, isApproved, offset, pageSize, sortField);
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
