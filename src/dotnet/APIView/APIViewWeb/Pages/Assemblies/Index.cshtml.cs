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
using Microsoft.VisualStudio.Services.Common;
using APIViewWeb.Hubs;
using Microsoft.AspNetCore.SignalR;
using APIViewWeb.LeanModels;
using System.Text;
using APIViewWeb.Managers.Interfaces;
using System.IO;
using ApiView;
using Microsoft.AspNetCore.Http;

namespace APIViewWeb.Pages.Assemblies
{
    public class IndexPageModel : PageModel
    {
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IHubContext<SignalRHub> _notificationHubContext;
        public readonly UserPreferenceCache _preferenceCache;
        public readonly IUserProfileManager _userProfileManager;
        private readonly ICodeFileManager _codeFileManager;

        public const int _defaultPageSize = 50;
        public const string _defaultSortField = "LastUpdatedOn";

        public IndexPageModel(IReviewManager reviewManager, IAPIRevisionsManager apiRevisionsManager, IUserProfileManager userProfileManager,
            UserPreferenceCache preferenceCache, IHubContext<SignalRHub> notificationHub, ICodeFileManager codeFileManager)
        {
            _notificationHubContext = notificationHub;
            _reviewManager = reviewManager;
            _apiRevisionsManager = apiRevisionsManager;
            _preferenceCache = preferenceCache;
            _userProfileManager = userProfileManager;
            _codeFileManager = codeFileManager;
        }
        [FromForm]
        public UploadModel Upload { get; set; }
        [FromForm]
        public string Label { get; set; }

        public ReviewsProperties ReviewsProperties { get; set; } = new ReviewsProperties();

        public (IEnumerable<ReviewListItemModel> Reviews, int TotalCount, int TotalPages,
            int CurrentPage, int? PreviousPage, int? NextPage) PagedResults { get; set; }
        [BindProperty(Name = "notificationMessage", SupportsGet = true)]
        public string NotificationMessage { get; set; }

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
            var review = await GetOrCreateReview(file, Upload.FilePath);

            if (review != null)
            {
                APIRevisionListItemModel apiRevision = null;

                if (file != null)
                {
                    using (var openReadStream = file.OpenReadStream())
                    {
                        apiRevision = await _apiRevisionsManager.AddAPIRevisionAsync(user: User, review: review, apiRevisionType: APIRevisionType.Manual,
                            name: file.FileName, label: Label, fileStream: openReadStream, language: Upload.Language);
                    }
                }
                else if (!string.IsNullOrEmpty(Upload.FilePath))
                {
                    apiRevision = await _apiRevisionsManager.AddAPIRevisionAsync(user: User, review: review, apiRevisionType: APIRevisionType.Manual,
                               name: file.FileName, label: Label, fileStream: null, language: Upload.Language);
                }
                return RedirectToPage("Review", new { id = review.Id, revisionId = apiRevision.Id });
            }
            return RedirectToPage();
        }

        private async Task<ReviewListItemModel> GetOrCreateReview(IFormFile file, string filePath)
        {
            CodeFile codeFile = null;
            ReviewListItemModel review = null;

            using var memoryStream = new MemoryStream();
            if (file != null)
            {
                using (var openReadStream = file.OpenReadStream())
                {
                    codeFile = await _codeFileManager.CreateCodeFileAsync(
                        originalName: file?.FileName, fileStream: openReadStream, runAnalysis: Upload.RunAnalysis, memoryStream: memoryStream, language: Upload.Language);
                }
            }
            else if (!string.IsNullOrEmpty(filePath))
            {
                codeFile = await _codeFileManager.CreateCodeFileAsync(
                    originalName: Upload.FilePath, runAnalysis: Upload.RunAnalysis, memoryStream: memoryStream, language: Upload.Language);
            }

            if (codeFile != null)
            {
                review = await _reviewManager.GetReviewAsync(packageName: codeFile.PackageName, language: codeFile.Language);
                if (review == null)
                {
                    review = await _reviewManager.CreateReviewAsync(packageName: codeFile.PackageName, language: codeFile.Language, isClosed: false);
                }
            }
            return review;
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
