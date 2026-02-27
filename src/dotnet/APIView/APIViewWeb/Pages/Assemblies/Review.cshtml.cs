using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.TeamFoundation.Common;

namespace APIViewWeb.Pages.Assemblies
{
    public class ReviewPageModel : PageModel
    {
        private static int REVIEW_DIFF_CONTEXT_SIZE = 3;
        private const string DIFF_CONTEXT_SEPERATOR = "<br><span>.....</span><br>";
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IPullRequestManager _pullRequestManager;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        private readonly ICommentsManager _commentsManager;
        private readonly INotificationManager _notificationManager;
        public readonly UserProfileCache _userProfileCache;
        private readonly ICosmosUserProfileRepository _userProfileRepository;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly IEnumerable<LanguageService> _languageServices;
        private readonly IPermissionsManager _permissionsManager;

        public ReviewPageModel(
            IReviewManager reviewManager,
            IAPIRevisionsManager reviewRevisionManager,
            IPullRequestManager pullRequestManager,
            IBlobCodeFileRepository codeFileRepository,
            ICommentsManager commentsManager,
            INotificationManager notificationManager,
            UserProfileCache userProfileCache,
            ICosmosUserProfileRepository userProfileRepository,
            IConfiguration configuration,
            IHubContext<SignalRHub> signalRHub,
            IEnumerable<LanguageService> languageServices,
            IPermissionsManager permissionsManager)
        {
            _reviewManager = reviewManager;
            _apiRevisionsManager = reviewRevisionManager;
            _pullRequestManager = pullRequestManager;
            _codeFileRepository = codeFileRepository;
            _commentsManager = commentsManager;
            _notificationManager = notificationManager;
            _userProfileCache = userProfileCache;
            _userProfileRepository = userProfileRepository;
            _configuration = configuration;
            _signalRHubContext = signalRHub;
            _languageServices = languageServices;
            _permissionsManager = permissionsManager;
        }

        public ReviewContentModel ReviewContent { get; set; }
        public ReviewCommentsModel Comments { get; set; }
        [BindProperty(SupportsGet = true)]
        public string DiffRevisionId { get; set; }
        // Flag to decide whether to  include documentation
        [BindProperty(Name = "doc", SupportsGet = true)]
        public bool? ShowDocumentation { get; set; }
        [BindProperty(Name = "diffOnly", SupportsGet = true)]
        public bool ShowDiffOnly { get; set; }
        [BindProperty(Name = "notificationMessage", SupportsGet = true)]
        public string NotificationMessage { get; set; }
        public UserPreferenceModel UserPreference { get; set; }

        /// <summary>
        /// Handler for loading page
        /// </summary>
        /// <param name="id"></param>
        /// <param name="revisionId"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnGetAsync(string id, string revisionId = null)
        {
            TempData["Page"] = "api";

            await GetReviewPageModelPropertiesAsync(id, revisionId, DiffRevisionId, ShowDiffOnly);

            ReviewContent = await PageModelHelpers.GetReviewContentAsync(configuration: _configuration,
                reviewManager: _reviewManager, userProfileCache: _userProfileCache, reviewRevisionsManager: _apiRevisionsManager,
                commentManager: _commentsManager, codeFileRepository: _codeFileRepository, signalRHubContext: _signalRHubContext,
                permissionsManager: _permissionsManager, user: User, reviewId: id, revisionId: revisionId, diffRevisionId: DiffRevisionId, showDocumentation: (ShowDocumentation ?? false),
                showDiffOnly: ShowDiffOnly, diffContextSize: REVIEW_DIFF_CONTEXT_SIZE, diffContextSeperator: DIFF_CONTEXT_SEPERATOR);

            if (ReviewContent.Directive == ReviewContentModelDirective.RedirectToSPAUI)
            {
                var uri = ManagerHelpers.ResolveReviewUrl(reviewId: id, apiRevisionId: ReviewContent.ActiveAPIRevision.Id,
                    language: ReviewContent.ActiveAPIRevision.Language, configuration: _configuration, languageServices: _languageServices, diffRevisionId: DiffRevisionId);
                return Redirect(uri);
            }

            if (ReviewContent.Directive == ReviewContentModelDirective.TryGetlegacyReview)
            {
                // Check if you can get review from legacy data
                var legacyReview = await _reviewManager.GetLegacyReviewAsync(User, id);
                if (legacyReview != null)
                {
                    var legacyRevision = legacyReview.Revisions.FirstOrDefault(r =>
                        !string.IsNullOrEmpty(r.Files[0].Language) && !string.IsNullOrEmpty(r.Files[0].PackageName));

                    if (legacyRevision == null)
                    {
                        return RedirectToPage("Index", new { notificationMessage = $"Review with ID : {id} was not found." });
                    }
                        
                    var review = await _reviewManager.GetReviewAsync(language: legacyRevision.Files[0].Language,
                        packageName: legacyRevision.Files[0].PackageName);

                    if (review != null)
                    {
                        var uri = Request.GetUri().ToString();
                        uri = uri.Replace(id, review.Id);
                        return Redirect(uri);
                    }
                }
                else
                {
                    return RedirectToPage("Index", new { notificationMessage = $"Review with ID : {id} was not found." });
                }
            }

            if (ReviewContent.Directive == ReviewContentModelDirective.ErrorDueToInvalidAPIRevisonProceedWithPageLoad)
            {
                NotificationMessage = ReviewContent.NotificationMessage;
            }

            if (ReviewContent.Directive == ReviewContentModelDirective.ErrorDueToInvalidAPIRevisonRedirectToIndexPage)
            {
                return RedirectToPage("Index", new { notificationMessage = ReviewContent.NotificationMessage });
            }

            if (ReviewContent.APIRevisions == null || !ReviewContent.APIRevisions.Any())
            {
                return RedirectToPage("LegacyReview", new { id = id });
            }

            if (!String.IsNullOrEmpty(ReviewContent.ActiveAPIRevision.Files.First().CrossLanguagePackageId))
            {
                var correspondingReviewId = await _apiRevisionsManager.GetReviewIdsOfLanguageCorrespondingReviewAsync(ReviewContent.ActiveAPIRevision.Files.First().CrossLanguagePackageId);
                var correspondingReviews = await _reviewManager.GetReviewsAsync(reviewIds: correspondingReviewId.Where(_ => _ != id).ToList(), isClosed: false);
                foreach (var review in correspondingReviews)
                {
                    var reviewContent = await PageModelHelpers.GetReviewContentAsync(configuration: _configuration,
                        reviewManager: _reviewManager, userProfileCache: _userProfileCache, reviewRevisionsManager: _apiRevisionsManager,
                        commentManager: _commentsManager, codeFileRepository: _codeFileRepository, signalRHubContext: _signalRHubContext,
                        permissionsManager: _permissionsManager, user: User, review: review, revisionId: null, diffRevisionId: null, showDocumentation: (ShowDocumentation ?? false),
                        showDiffOnly: ShowDiffOnly, diffContextSize: REVIEW_DIFF_CONTEXT_SIZE, diffContextSeperator: DIFF_CONTEXT_SEPERATOR);

                    ReviewContent.CrossLanguageViewContent.Add(review.Language, reviewContent);
                }
            }

            return Page();
        }

        /// <summary>
        /// Gets CodeLine Section for pages with collapsible sections (Swagger Pages)
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sectionKey"></param>
        /// <param name="sectionKeyA"></param>
        /// <param name="sectionKeyB"></param>
        /// <param name="revisionId"></param>
        /// <param name="diffRevisionId"></param>
        /// <param name="diffOnly"></param>
        /// <returns></returns>
        public async Task<PartialViewResult> OnGetCodeLineSectionAsync(
            string id, int sectionKey, int? sectionKeyA = null, int? sectionKeyB = null,
            string revisionId = null, string diffRevisionId = null, bool diffOnly = false)
        {
            if (revisionId == null)
            {
                var apiRevision = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(reviewId: id, apiRevisionType: APIRevisionType.Automatic);
                revisionId = apiRevision.Id;
            }
            await GetReviewPageModelPropertiesAsync(id, revisionId, diffRevisionId, diffOnly);

            var codeLines = await PageModelHelpers.GetCodeLineSectionAsync(user: User, reviewManager: _reviewManager,
            apiRevisionsManager: _apiRevisionsManager, commentManager: _commentsManager,
            codeFileRepository: _codeFileRepository, reviewId: id, sectionKey: sectionKey, revisionId: revisionId,
            diffRevisionId: diffRevisionId, diffContextSize: REVIEW_DIFF_CONTEXT_SIZE, diffContextSeperator: DIFF_CONTEXT_SEPERATOR,
            sectionKeyA: sectionKeyA, sectionKeyB: sectionKeyB
            );

            TempData["CodeLineSection"] = codeLines;
            TempData["UserPreference"] = UserPreference;
            return Partial("_CodeLinePartial", sectionKey);
        }

        /// <summary>
        /// Get Revisions Partial
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionType"></param>
        /// <param name="showDoc"></param>
        /// <param name="showDiffOnly"></param>
        /// <returns></returns>
        public async Task<PartialViewResult> OnGetAPIRevisionsPartialAsync(string reviewId, APIRevisionType apiRevisionType, bool showDoc = false, bool showDiffOnly = false)
        {
            var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId);
            revisions = revisions.Where(r => r.APIRevisionType == apiRevisionType).OrderByDescending(c => c.CreatedOn).ToList();
            (IEnumerable<APIRevisionListItemModel> revisions, APIRevisionListItemModel activeRevision, APIRevisionListItemModel diffRevision, bool forDiff, bool showDocumentation, bool showDiffOnly) revisionSelectModel = (
                revisions: revisions,
                activeRevision: default(APIRevisionListItemModel),
                diffRevision: default(APIRevisionListItemModel),
                forDiff: false,
                showDocumentation: showDoc,
                showDiffOnly: showDiffOnly
            );
            return Partial("_RevisionSelectPickerPartial", revisionSelectModel);
        }

        /// <summary>
        /// Get Diff Revisions Partial
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionId"></param>
        /// <param name="apiRevisionType"></param>
        /// <param name="showDoc"></param>
        /// <param name="showDiffOnly"></param>
        /// <returns></returns>
        public async Task<PartialViewResult> OnGetAPIDiffRevisionsPartialAsync(string reviewId, string apiRevisionId, APIRevisionType apiRevisionType, bool showDoc = false, bool showDiffOnly = false)
        {
            var apiRevisions = await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId);
            if (apiRevisions.IsNullOrEmpty())
            {
                var notifcation = new NotificationModel() { Message = $"This review has no valid apiRevisons", Level = NotificatonLevel.Warning };
                await _signalRHubContext.Clients.Group(User.GetGitHubLogin()).SendAsync("RecieveNotification", notifcation);
            }

            APIRevisionListItemModel activeRevision = default(APIRevisionListItemModel);

            if (!Guid.TryParse(apiRevisionId, out _))
            {
                activeRevision = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(reviewId, apiRevisions);
            }
            else
            {
                activeRevision = apiRevisions.FirstOrDefault(r => r.Id == apiRevisionId);
            }

            var revisionsForDiff = apiRevisions.Where(r => r.APIRevisionType == apiRevisionType && r.Id != activeRevision.Id).OrderByDescending(c => c.CreatedOn).ToList();

            (IEnumerable<APIRevisionListItemModel> revisions, APIRevisionListItemModel activeRevision, APIRevisionListItemModel diffRevision, bool forDiff, bool showDocumentation, bool showDiffOnly) revisionSelectModel = (
                revisions: revisionsForDiff,
                activeRevision: activeRevision,
                diffRevision: default(APIRevisionListItemModel),
                forDiff: true,
                showDocumentation: showDoc,
                showDiffOnly: showDiffOnly
            );
            return Partial("_RevisionSelectPickerPartial", revisionSelectModel);
        }

        /// <summary>
        /// Toggle Review State
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<ActionResult> OnPostToggleClosedAsync(string id)
        {
            await _reviewManager.ToggleReviewIsClosedAsync(User, id);
            return RedirectToPage(new { id = id });
        }

        /// <summary>
        /// Subscribe or UnSubscribe to a Review
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<ActionResult> OnPostToggleSubscribedAsync(string id)
        {
            await _notificationManager.ToggleSubscribedAsync(User, id);
            return RedirectToPage(new { id = id });
        }

        /// <summary>
        /// Approve or Revert Approval for a Review
        /// </summary>
        /// <param name="id"></param>
        /// <param name="revisionId"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnPostToggleReviewApprovalAsync(string id, string revisionId)
        {
            await _reviewManager.ToggleReviewApprovalAsync(User, id, revisionId);
            return RedirectToPage(new { id = id, revisionId = revisionId });
        }

        /// <summary>
        /// Approve or Revert Approval for a Revision
        /// </summary>
        /// <param name="id"></param>
        /// <param name="revisionId"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnPostToggleAPIRevisionApprovalAsync(string id, string revisionId)
        {
            (var updateReview, var apiRevision) = await _apiRevisionsManager.ToggleAPIRevisionApprovalAsync(User, id, revisionId);
            if (updateReview)
            {
                await OnPostToggleReviewApprovalAsync(id, revisionId);
            }
            return RedirectToPage(new { id = id, revisionId = revisionId });
        }

        /// <summary>
        /// Request Reviewers for aa APIRevision
        /// </summary>
        /// <param name="id"></param>
        /// <param name="apiRevisionId"></param>
        /// <param name="reviewers"></param>
        /// <returns></returns>
        public async Task<ActionResult> OnPostRequestReviewersAsync(string id, string apiRevisionId, HashSet<string> reviewers)
        {
            var currentApiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(User, apiRevisionId);
            var existingReviewers = new HashSet<string>(
                currentApiRevision.AssignedReviewers.Select(assignment => assignment.AssingedTo),
                StringComparer.OrdinalIgnoreCase);

            await _apiRevisionsManager.AssignReviewersToAPIRevisionAsync(User, apiRevisionId, reviewers);

            var newlyAddedReviewers = reviewers
                .Where(reviewer => !existingReviewers.Contains(reviewer))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            await _notificationManager.NotifyAssignedReviewersAsync(User, apiRevisionId, newlyAddedReviewers);
            return RedirectToPage(new { id = id, revisionId = apiRevisionId });
        }

        /// <summary>
        /// Upload APIRevisions
        /// </summary>
        /// <param name="id"></param>
        /// <param name="upload"></param>
        /// <param name="label"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnPostUploadAsync(string id, [FromForm] IFormFile upload, [FromForm] string label, [FromForm] string filePath)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage();
            }

            var apiRevision = await PageModelHelpers.UploadAPIRevisionAsync(_apiRevisionsManager, User, id, upload, label, filePath);

            return RedirectToPage(new { id = id, revisionId = apiRevision.Id });
        }

        /// <summary>
        /// Get Routing Data for a Review
        /// </summary>
        /// <param name="diffRevisionId"></param>
        /// <param name="showDiffOnly"></param>
        /// <param name="showDocumentation"></param>
        /// <param name="revisionId"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetRoutingData(string diffRevisionId = null, bool? showDiffOnly = null, bool? showDocumentation = null, string revisionId = null)
        {
            var routingData = new Dictionary<string, string>();
            routingData["revisionId"] = revisionId;
            routingData["diffRevisionId"] = diffRevisionId;
            routingData["doc"] = (showDocumentation ?? false).ToString();
            routingData["diffOnly"] = (showDiffOnly ?? false).ToString();
            return routingData;
        }

        /// <summary>
        /// Get Pull Requests for a Review
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<PullRequestModel>> GetAssociatedPullRequest()
        {
            return await _pullRequestManager.GetPullRequestsModelAsync(reviewId: ReviewContent.Review.Id, apiRevisionId: ReviewContent.ActiveAPIRevision.Id);
        }

        /// <summary>
        /// Get PR of Associated Reviews
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<PullRequestModel>> GetPRsOfAssoicatedReviews()
        {
            var creatingPR = (await _pullRequestManager.GetPullRequestsModelAsync(reviewId: ReviewContent.Review.Id, apiRevisionId: ReviewContent.ActiveAPIRevision.Id)).FirstOrDefault();
            if (creatingPR != null)
            {
                return await _pullRequestManager.GetPullRequestsModelAsync(creatingPR.PullRequestNumber, creatingPR.RepoName);
            }
            return new List<PullRequestModel>();
        }

        /// <summary>
        /// Get Review Page Model Properties
        /// </summary>
        /// <param name="id"></param>
        /// <param name="revisionId"></param>
        /// <param name="diffRevisionId"></param>
        /// <param name="diffOnly"></param>
        /// <returns></returns>
        private async Task GetReviewPageModelPropertiesAsync(string id, string revisionId = null, string diffRevisionId = null, bool diffOnly = false)
        {
            var userProfile = await _userProfileCache.GetUserProfileAsync(User.GetGitHubLogin());
            Comments = await _commentsManager.GetReviewCommentsAsync(id);
            DiffRevisionId = (DiffRevisionId == null) ? diffRevisionId : DiffRevisionId;
            ShowDiffOnly = (ShowDiffOnly == false) ? diffOnly : ShowDiffOnly;

            if (ShowDocumentation.HasValue)
            {
                userProfile.Preferences.ShowDocumentation = ShowDocumentation.Value;
                await _userProfileCache.UpdateUserProfileAsync(User.GetGitHubLogin(), userProfile.Email, userProfile.Preferences);
            }
            else
            {
                ShowDocumentation = userProfile.Preferences.ShowDocumentation;
            }
            UserPreference = userProfile.Preferences;
        }

        /// <summary>
        /// Get Data for BS Target
        /// </summary>
        /// <param name="hasActiveConversations"></param>
        /// <param name="hasFatalDiagnostics"></param>
        /// <param name="userIsLanguageApprover"></param>
        /// <param name="isActiveRevisionAhead"></param>
        /// <returns></returns>

        public string GetDataBSTarget(bool hasActiveConversations, bool hasFatalDiagnostics, bool userIsLanguageApprover, bool isActiveRevisionAhead)
        {
            if (hasActiveConversations && hasFatalDiagnostics && userIsLanguageApprover && isActiveRevisionAhead)
            {
                return "#convoFatalModel";
            }
            else if (hasActiveConversations && !hasFatalDiagnostics && userIsLanguageApprover && isActiveRevisionAhead)
            {
                return "#openConversationModel";
            }
            else if (!hasActiveConversations && hasFatalDiagnostics && userIsLanguageApprover && isActiveRevisionAhead)
            {
                return "#fatalErrorModel";
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
