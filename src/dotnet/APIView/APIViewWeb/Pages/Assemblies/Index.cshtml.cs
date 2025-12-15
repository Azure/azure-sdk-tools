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
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using Microsoft.AspNetCore.SignalR;
using APIViewWeb.LeanModels;
using System.Text;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.DTOs;

namespace APIViewWeb.Pages.Assemblies
{
    public class IndexPageModel : PageModel
    {
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IHubContext<SignalRHub> _notificationHubContext;
        public readonly UserProfileCache _userProfileCache;
        private readonly ICodeFileManager _codeFileManager;

        public const int _defaultPageSize = 50;
        public const string _defaultSortField = "LastUpdatedOn";

        public IndexPageModel(IReviewManager reviewManager, IAPIRevisionsManager apiRevisionsManager, IUserProfileManager userProfileManager,
            UserProfileCache userProfileCache, IHubContext<SignalRHub> notificationHub, ICodeFileManager codeFileManager)
        {
            _notificationHubContext = notificationHub;
            _reviewManager = reviewManager;
            _apiRevisionsManager = apiRevisionsManager;
            _userProfileCache = userProfileCache;
            _codeFileManager = codeFileManager;
        }
        [FromForm]
        public UploadModel Upload { get; set; }
        [FromForm]
        public string Label { get; set; }

        public ReviewsProperties ReviewsProperties { get; set; } = new ReviewsProperties();

        public (IEnumerable<ReviewListItemModel> Reviews, int TotalCount, int TotalPages,
            int CurrentPage, int? PreviousPage, int? NextPage) PagedResults { get; set; }
        
        public string SearchQuery { get; set; } = string.Empty;
        [BindProperty(Name = "notificationMessage", SupportsGet = true)]
        public string NotificationMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(
            IEnumerable<string> search, IEnumerable<string> languages, IEnumerable<string> state,
            IEnumerable<string> status, int pageNo=1, int pageSize=_defaultPageSize, string sortField=_defaultSortField)
        {
            await _userProfileCache.SetUserEmailIfNullOrEmpty(User);
            var userPreference = (await _userProfileCache.GetUserProfileAsync(User.GetGitHubLogin())).Preferences;

            if (!search.Any() && !languages.Any() && !state.Any() && !status.Any())
            {
                languages = userPreference.Language;
                state = userPreference.State;
                status = userPreference.Status;
                await RunGetRequest(search, languages, state, status, pageNo, pageSize, sortField, false);
            }
            else 
            {
                await RunGetRequest(search, languages, state, status, pageNo, pageSize, sortField);
            }
            return Page();
        }

        public async Task<PartialViewResult> OnGetReviewsPartialAsync(
            IEnumerable<string> search, IEnumerable<string> languages, IEnumerable<string> state,
            IEnumerable<string> status, int pageNo = 1, int pageSize=_defaultPageSize, string sortField=_defaultSortField)
        {
            await RunGetRequest(search, languages, state, status, pageNo, pageSize, sortField);
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
            ReviewListItemModel review = null;

            if (!string.IsNullOrEmpty(Upload.PackageName))
            {
                review = await _reviewManager.GetReviewAsync(language: Upload.Language, packageName: Upload.PackageName);
            }

            if (review == null)
            {
                review = await _reviewManager.GetOrCreateReview(file: file, filePath: Upload.FilePath, language: Upload.Language);
            }

            if (review != null)
            {
                APIRevisionListItemModel apiRevision = await _apiRevisionsManager.CreateAPIRevisionAsync(user: User, review: review, file: file, filePath: Upload.FilePath, language: Upload.Language, label: Label);
                return RedirectToPage("Review", new { id = review.Id, revisionId = apiRevision.Id });
            }
            return RedirectToPage();
        }

        private async Task RunGetRequest(IEnumerable<string> search, IEnumerable<string> languages,
            IEnumerable<string> state, IEnumerable<string> status, int pageNo, int pageSize, string sortField, bool fromUrl = true)
        {
            search = search.Select(x => HttpUtility.UrlDecode(x));
            languages = (fromUrl)? languages.Select(x => HttpUtility.UrlDecode(x)) : languages;
            state = state.Select(x => HttpUtility.UrlDecode(x));
            status = status.Select(x => HttpUtility.UrlDecode(x));

            // Set search query for the view
            SearchQuery = search != null ? string.Join(" ", search) : string.Empty;

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

            await _userProfileCache.UpdateUserProfileAsync(userName: User.GetGitHubLogin(), userPreferenceDto: new UserPreferenceDto {
                Language = languages,
                State = state,
                Status = status
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
