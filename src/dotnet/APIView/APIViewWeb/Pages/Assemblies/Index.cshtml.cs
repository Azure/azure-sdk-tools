using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using APIViewWeb.Managers;
using Microsoft.TeamFoundation.Common;
using APIViewWeb.Helpers;
using Microsoft.VisualStudio.Services.Common;
using APIViewWeb.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Text;

namespace APIViewWeb.Pages.Assemblies
{
    public class IndexPageModel : PageModel
    {
        private readonly IReviewManager _manager;
        private readonly IHubContext<SignalRHub> _notificationHubContext;
        public readonly UserPreferenceCache _preferenceCache;
        public readonly IUserProfileManager _userProfileManager;
        public const int _defaultPageSize = 50;
        public const string _defaultSortField = "LastUpdated";

        public IndexPageModel(IReviewManager manager, IUserProfileManager userProfileManager, UserPreferenceCache preferenceCache, IHubContext<SignalRHub> notificationHub)
        {
            _notificationHubContext = notificationHub;
            _manager = manager;
            _preferenceCache = preferenceCache;
            _userProfileManager = userProfileManager;
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
                UserPreferenceModel userPreference = await _preferenceCache.GetUserPreferences(User);
                languages = userPreference.Language;
                state = userPreference.State;
                status = userPreference.Status;
                type = userPreference.FilterType.Select(x => x.ToString());
                await RunGetRequest(search, languages, state, status, type, pageNo, pageSize, sortField, false);
            }
            else 
            {
                await RunGetRequest(search, languages, state, status, type, pageNo, pageSize, sortField);
            }
        }

        public async Task<PartialViewResult> OnGetReviewsPartialAsync(
            IEnumerable<string> search, IEnumerable<string> languages, IEnumerable<string> state,
            IEnumerable<string> status, IEnumerable<string> type, int pageNo = 1, int pageSize=_defaultPageSize, string sortField=_defaultSortField)
        {
            await RunGetRequest(search, languages, state, status, type, pageNo, pageSize, sortField);
            return Partial("_ReviewsPartial", PagedResults);
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            if (!ModelState.IsValid)
            {
                var errors = new StringBuilder();
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        errors.AppendLine(error.ErrorMessage);
                    }
                }
                var notifcation = new NotificationModel() { Message = errors.ToString(), Level = NotificatonLevel.Error };
                await _notificationHubContext.Clients.Group(User.GetGitHubLogin()).SendAsync("RecieveNotification", notifcation);
                return new NoContentResult();
            }

            var file = Upload.Files?.SingleOrDefault();

            if (file != null)
            {
                using (var openReadStream = file.OpenReadStream())
                {
                    var reviewModel = await _manager.CreateReviewAsync(User, file.FileName, Label, openReadStream, Upload.RunAnalysis, langauge: Upload.Language);
                    return RedirectToPage("Review", new { id = reviewModel.ReviewId });
                }
            }
            else if (!Upload.FilePath.IsNullOrEmpty())
            {
                var reviewModel = await _manager.CreateReviewAsync(User, Upload.FilePath, Label, null, Upload.RunAnalysis, langauge: Upload.Language);
                return RedirectToPage("Review", new { id = reviewModel.ReviewId });
            }

            return RedirectToPage();
        }

        private async Task RunGetRequest(IEnumerable<string> search, IEnumerable<string> languages,
            IEnumerable<string> state, IEnumerable<string> status, IEnumerable<string> type, int pageNo, int pageSize, string sortField, bool fromUrl = true)
        {
            search = search.Select(x => HttpUtility.UrlDecode(x));
            languages = (fromUrl)? languages.Select(x => HttpUtility.UrlDecode(x)) : languages;
            state = state.Select(x => HttpUtility.UrlDecode(x));
            status = status.Select(x => HttpUtility.UrlDecode(x));
            type = type.Select(x => HttpUtility.UrlDecode(x));

            // Update selected properties
            if (languages.Any())
            {
                ReviewsProperties.Languages.Selected = languages;
            }

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
            }, User);

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

            languages = LanguageServiceHelpers.MapLanguageAliases(languages);

            PagedResults = await _manager.GetPagedReviewsAsync(search, languages, isClosed, filterTypesAsInt, isApproved, offset, pageSize, sortField);
        }
    }

    public class ReviewsProperties 
    {
        public (IEnumerable<string> All, IEnumerable<string> Selected) Languages = (All: LanguageServiceHelpers.SupportedLanguages, Selected: new List<string>());
        public (IEnumerable<string> All, IEnumerable<string> Selected) State = (All: new List<string> { "Closed", "Open" }, Selected: new List<string> { "Open" });
        public (IEnumerable<string> All, IEnumerable<string> Selected) Status = (All: new List<string> { "Approved", "Pending" }, Selected: new List<string>());
        public (IEnumerable<string> All, IEnumerable<string> Selected) Type = (All: new List<string> { "Automatic", "Manual", "PullRequest" }, Selected: new List<string>());
    }
}
