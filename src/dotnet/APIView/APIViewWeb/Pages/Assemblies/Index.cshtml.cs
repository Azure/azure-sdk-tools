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
using APIViewWeb.LeanModels;

namespace APIViewWeb.Pages.Assemblies
{
    public class IndexPageModel : PageModel
    {
        private readonly IReviewManager _reviewManager;
        private readonly IHubContext<SignalRHub> _notificationHubContext;
        public readonly UserPreferenceCache _preferenceCache;
        public readonly IUserProfileManager _userProfileManager;
        public const int _defaultPageSize = 50;
        public const string _defaultSortField = "LastUpdatedOn";

        public IndexPageModel(IReviewManager reviewManager, IUserProfileManager userProfileManager, UserPreferenceCache preferenceCache, IHubContext<SignalRHub> notificationHub)
        {
            _notificationHubContext = notificationHub;
            _reviewManager = reviewManager;
            _preferenceCache = preferenceCache;
            _userProfileManager = userProfileManager;
        }

        public ReviewsProperties ReviewsProperties { get; set; } = new ReviewsProperties();

        public (IEnumerable<ReviewListItemModel> Reviews, int TotalCount, int TotalPages,
            int CurrentPage, int? PreviousPage, int? NextPage) PagedResults { get; set; }

        public async Task OnGetAsync(
            IEnumerable<string> search, IEnumerable<string> languages, IEnumerable<string> state,
            IEnumerable<string> status, int pageNo=1, int pageSize=_defaultPageSize, string sortField=_defaultSortField)
        {
            if (!search.Any() && !languages.Any() && !state.Any() && !status.Any())
            {
                UserPreferenceModel userPreference = await _preferenceCache.GetUserPreferences(User);
                languages = userPreference.Language;
                state = userPreference.State;
                status = userPreference.Status;
                await RunGetRequest(search, languages, state, status, pageNo, pageSize, sortField, false);
            }
            else 
            {
                await RunGetRequest(search, languages, state, status, pageNo, pageSize, sortField);
            }
        }

        public async Task<PartialViewResult> OnGetReviewsPartialAsync(
            IEnumerable<string> search, IEnumerable<string> languages, IEnumerable<string> state,
            IEnumerable<string> status, int pageNo = 1, int pageSize=_defaultPageSize, string sortField=_defaultSortField)
        {
            await RunGetRequest(search, languages, state, status, pageNo, pageSize, sortField);
            return Partial("_ReviewsPartial", PagedResults);
        }

        private async Task RunGetRequest(IEnumerable<string> search, IEnumerable<string> languages,
            IEnumerable<string> state, IEnumerable<string> status, int pageNo, int pageSize, string sortField, bool fromUrl = true)
        {
            search = search.Select(x => HttpUtility.UrlDecode(x));
            languages = (fromUrl)? languages.Select(x => HttpUtility.UrlDecode(x)) : languages;
            state = state.Select(x => HttpUtility.UrlDecode(x));
            status = status.Select(x => HttpUtility.UrlDecode(x));

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

            _preferenceCache.UpdateUserPreference(new UserPreferenceModel {
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

            PagedResults = await _reviewManager.GetPagedReviewListAsync(search, languages, isClosed, isApproved, offset, pageSize, sortField);
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
