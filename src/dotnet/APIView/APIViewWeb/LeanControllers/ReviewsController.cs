using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Extensions;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using APIViewWeb.DTOs;

namespace APIViewWeb.LeanControllers
{
    public class ReviewsController : BaseApiController
    {
        private readonly ILogger<ReviewsController> _logger;
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly ICommentsManager _commentsManager;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        private readonly IConfiguration _configuration;
        public readonly UserProfileCache _userProfileCache;
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly INotificationManager _notificationManager;
        private readonly IEnumerable<LanguageService> _languageServices;
        private readonly IPermissionsManager _permissionsManager;

        public ReviewsController(ILogger<ReviewsController> logger,
            IAPIRevisionsManager reviewRevisionsManager, 
            IReviewManager reviewManager,
            ICommentsManager commentManager,
            IBlobCodeFileRepository codeFileRepository,
            IConfiguration configuration, 
            UserProfileCache userProfileCache,
            IEnumerable<LanguageService> languageServices,
            IHubContext<SignalRHub> signalRHub, 
            INotificationManager notificationManager,
            IPermissionsManager permissionsManager)
        {
            _logger = logger;
            _apiRevisionsManager = reviewRevisionsManager;
            _reviewManager = reviewManager;
            _commentsManager = commentManager;
            _codeFileRepository = codeFileRepository;
            _configuration = configuration;
            _userProfileCache = userProfileCache;
            _languageServices = languageServices;
            _signalRHubContext = signalRHub;
            _notificationManager = notificationManager;
            _permissionsManager = permissionsManager;
        }

        /// <summary>
        /// Retrieves a list of reviews grouped by pages
        /// </summary>
        /// <param name="pageParams"></param>
        /// <param name="filterAndSortParams"></param>
        /// <returns></returns>
        [HttpGet(Name = "GetReviews")]
        public async Task<ActionResult<PagedList<ReviewListItemModel>>> GetReviewsAsync([FromQuery] PageParams pageParams, [FromQuery] FilterAndSortParams filterAndSortParams)
        {
            var result = await _reviewManager.GetReviewsAsync(pageParams, filterAndSortParams);
            Response.AddPaginationHeader(new PaginationHeader(result.NoOfItemsRead, result.PageSize, result.TotalCount));
            return new LeanJsonResult(result, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Retrieves distinct package names for a language
        /// </summary>
        /// <param name="language">The language to filter by</param>
        /// <returns>A list of distinct package names</returns>
        [HttpGet("languages/{language}/packagenames", Name = "GetPackageNames")]
        public async Task<ActionResult<IEnumerable<string>>> GetPackageNamesAsync(string language)
        {
            var result = await _reviewManager.GetPackageNamesAsync(language);
            return new LeanJsonResult(result, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Retrieves a review by its id
        /// </summary>
        /// <param name="reviewId"></param>
        /// <returns></returns>
        [HttpGet("{reviewId}", Name = "GetReview")]
        public async Task<ActionResult<ReviewListItemModel>> GetReviewAsync(string reviewId)
        {
            var review = await _reviewManager.GetReviewAsync(User, reviewId);
            if (review != null)
            {
                return new LeanJsonResult(review, StatusCodes.Status200OK);
            }
            return StatusCode(StatusCodes.Status404NotFound);
        }

        [HttpGet("enableNamespaceReview", Name = "EnableNamespaceReview")]
        public ActionResult<bool> IsNamespaceReviewEnabled()
        {
            var enableNamespaceReview = _configuration["EnableNamespaceReview"];
            return new LeanJsonResult(enableNamespaceReview, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Create a Reviews
        /// </summary>
        /// <param name="reviewCreationParam"></param>
        /// <returns></returns>
        [HttpPost(Name = "CreateReview")]
        public async Task<ActionResult<APIRevisionListItemModel>> CreateReviewAsync([FromForm] ReviewCreationParam reviewCreationParam)
        {
            var review = await _reviewManager.GetOrCreateReview(file: reviewCreationParam.File, filePath: reviewCreationParam.FilePath, language: reviewCreationParam.Language);

            if (review != null)
            {
                APIRevisionListItemModel apiRevision = await _apiRevisionsManager.CreateAPIRevisionAsync(user: User, review: review, file: reviewCreationParam.File, 
                    filePath: reviewCreationParam.FilePath, language: reviewCreationParam.Language, label: reviewCreationParam.Label);
                return new LeanJsonResult(apiRevision, StatusCodes.Status201Created);
            }
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        /// <summary>
        /// Endpoint used by Client SPA for Toggling Review Approval
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionId"></param>
        /// <param name="approvalRequest"></param>
        /// <returns></returns>
        [HttpPost("{reviewId}/{apiRevisionId}", Name = "ToggleReviewApproval")]
        public async Task<ActionResult> ToggleReviewApprovalAsync(string reviewId, string apiRevisionId, [FromBody] ApprovalRequest approvalRequest)
        {
            ReviewListItemModel currentReview = await _reviewManager.GetReviewAsync(User, reviewId);
            if (currentReview.IsApproved == approvalRequest.Approve)
            {
                return new LeanJsonResult(currentReview, StatusCodes.Status200OK);
            }

            ReviewListItemModel updatedReview = await _reviewManager.ToggleReviewApprovalAsync(User, reviewId, apiRevisionId);
            await _signalRHubContext.Clients.All.SendAsync("ReviewUpdated", updatedReview);
            return new LeanJsonResult(updatedReview, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Endpoint used by Client SPA for Requesting Namespace Review
        /// </summary>
        /// <param name="reviewId">The TypeSpec review ID to request namespace approval for</param>
        /// <param name="activeApiRevisionId">The active API revision ID</param>
        /// <returns></returns>
        [HttpPost("{reviewId}/requestNamespaceReview/{activeApiRevisionId}", Name = "RequestNamespaceReview")]
        public async Task<ActionResult> RequestNamespaceReviewAsync(string reviewId, string activeApiRevisionId)
        {
            try
            {
                if (string.IsNullOrEmpty(activeApiRevisionId))
                {
                    return BadRequest("Active API revision ID is required");
                }

                var updatedReview = await _reviewManager.RequestNamespaceReviewAsync(User, reviewId, activeApiRevisionId);
                return new LeanJsonResult(updatedReview, StatusCodes.Status200OK);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error: " + ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Endpoint used by Client SPA toggling Subscription to a review
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="state"></param> true = subscribe, false = unsubscribe
        /// <returns></returns>
        [HttpPost("{reviewId}/toggleSubscribe", Name = "ToggleSubscribe")]
        public async Task<ActionResult<APIRevisionListItemModel>> ToggleSubscribeAsync(string reviewId, [FromQuery] bool state)
        {
            string userName = User.GetGitHubLogin();
            await _notificationManager.ToggleSubscribedAsync(User, reviewId, state);
            return Ok();
        }

        ///<summary>
        ///Retrieve the Content (codeLines and Navigation) of a review
        ///</summary>
        ///<param name="reviewId"></param>
        ///<param name="activeApiRevisionId"></param>
        /// <param name="diffApiRevisionId"></param>
        ///<returns></returns>
        [Route("{reviewId}/content")]
        [HttpGet]
        public async Task<ActionResult<CodePanelData>> GetReviewContentAsync(string reviewId, [FromQuery] string activeApiRevisionId,
            [FromQuery] string diffApiRevisionId = null)
        {
            var activeAPIRevision = await _apiRevisionsManager.GetAPIRevisionAsync(User, activeApiRevisionId);
            APIRevisionListItemModel diffAPIRevision = null;

            if (activeAPIRevision.IsDeleted)
            {
                return new LeanJsonResult(null, StatusCodes.Status204NoContent);
            }

            if (!string.IsNullOrEmpty(diffApiRevisionId))
            {
                diffAPIRevision = await _apiRevisionsManager.GetAPIRevisionAsync(User, diffApiRevisionId);

                if (diffAPIRevision.IsDeleted) 
                {
                    return new LeanJsonResult(null, StatusCodes.Status204NoContent);
                }
            }

            if (activeAPIRevision.Files[0].ParserStyle == ParserStyle.Tree)
            {

                Task<CodeFile> activeFileTask = _codeFileRepository.GetCodeFileFromStorageAsync(
                    revisionId: activeAPIRevision.Id, 
                    codeFileId: activeAPIRevision.Files[0].FileId);

                Task<CodeFile> diffFileTask = null;
                if (diffAPIRevision != null)
                {
                    diffFileTask = _codeFileRepository.GetCodeFileFromStorageAsync(
                        revisionId: diffAPIRevision.Id, 
                        codeFileId: diffAPIRevision.Files[0].FileId);
                }

                CodeFile activeRevisionReviewCodeFile;
                CodeFile diffRevisionCodeFile = null;
                
                if (diffFileTask != null)
                {
                    await Task.WhenAll(activeFileTask, diffFileTask);
                    activeRevisionReviewCodeFile = await activeFileTask;
                    diffRevisionCodeFile = await diffFileTask;
                }
                else
                {
                    activeRevisionReviewCodeFile = await activeFileTask;
                }
                
                if (activeRevisionReviewCodeFile.ContentGenerationInProgress)
                {
                    var languageServices = LanguageServiceHelpers.GetLanguageService(activeAPIRevision.Language, _languageServices);
                    return new LeanJsonResult("Content generation in progress", StatusCodes.Status202Accepted, languageServices.ReviewGenerationPipelineUrl);
                }

                IEnumerable<CommentItemModel> allCommentsFromDb = await _commentsManager.GetCommentsAsync(reviewId, commentType: CommentType.APIRevision);
                List<CommentItemModel> diagnosticComments = await _commentsManager.SyncDiagnosticCommentsAsync(
                    activeAPIRevision,
                    activeRevisionReviewCodeFile.Diagnostics,
                    allCommentsFromDb);

                // After sync, build the full comment set from non-diagnostic DB comments + freshly synced diagnostics,
                // then apply the shared visibility filter (same rules as Conversations panel & quality score).
                var allCommentsWithSyncedDiagnostics = allCommentsFromDb
                    .Where(c => c.CommentSource != CommentSource.Diagnostic)
                    .Concat(diagnosticComments);
                List<CommentItemModel> visibleComments = CommentVisibilityHelper.GetVisibleComments(allCommentsWithSyncedDiagnostics, activeApiRevisionId);

                // Code panel additionally excludes resolved comments from non-active revisions
                List<CommentItemModel> filteredComments = visibleComments.Where(c => !c.IsResolved || c.APIRevisionId == activeApiRevisionId).ToList();
                var codePanelRawData = new CodePanelRawData()
                {
                    activeRevisionCodeFile = activeRevisionReviewCodeFile,
                    Comments = filteredComments
                };

                if (diffRevisionCodeFile != null)
                {
                    codePanelRawData.diffRevisionCodeFile = diffRevisionCodeFile;
                }

                CodePanelData result = await CodeFileHelpers.GenerateCodePanelDataAsync(codePanelRawData);
                return new LeanJsonResult(result, StatusCodes.Status200OK);
            }

            return new LeanJsonResult("Invalid APIRevision", StatusCodes.Status500InternalServerError);
        }

        ///<summary>
        ///Retrieve Cross Language Content for specified revisions
        ///</summary>
        ///<param name="apiRevisionId"></param>
        ///<param name="apiCodeFileId"></param>
        ///<returns></returns>
        [Route("crossLanguageContent")]
        [HttpGet]
        public async Task<ActionResult<CrossLanguageContentDto>> GetReviewContentAsync([FromQuery] string apiRevisionId, [FromQuery] string apiCodeFileId)
        {
            var results = new List<CrossLanguageContentDto>();

            var revisionReviewCodeFile = await _codeFileRepository.GetCodeFileFromStorageAsync(revisionId: apiRevisionId, codeFileId: apiCodeFileId);
            var processingData = new CrossLanguageProcessingDto();
            await CodeFileHelpers.GrabCrossLanguageReviewLines(processingData, revisionReviewCodeFile.ReviewLines);
            var contentData = new CrossLanguageContentDto();
            contentData.Content = processingData.Content;
            contentData.APIRevisionId = apiRevisionId;
            contentData.Language = revisionReviewCodeFile.Language;

            return new LeanJsonResult(contentData, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Get whether Copilot review is required for approval
        /// </summary>
        /// <param name="language">The programming language of the review</param>
        /// <returns>Boolean indicating if Copilot review is required</returns>
        [HttpGet("isReviewByCopilotRequired")]
        public ActionResult<bool> GetIsReviewByCopilotRequired([FromQuery] string language)
        {
            string value = _configuration["CopilotReviewIsRequired"];

            if (string.IsNullOrEmpty(value))
            {
                return Ok(false);
            }

            if (bool.TryParse(value, out bool isRequired))
            {
                return Ok(isRequired);
            }

            // If value is "*", Copilot review is required for all languages
            if (value.Trim() == "*")
            {
                return Ok(true);
            }
            
            if (!string.IsNullOrEmpty(language))
            {
                string[] supportedLanguages = value.Split(',')
                    .Where(lang => !string.IsNullOrEmpty(lang))
                    .Select(lang => lang.Trim().ToLowerInvariant())
                    .ToArray();
                
                string normalizedLanguage = language.Trim().ToLowerInvariant();
                return Ok(supportedLanguages.Contains(normalizedLanguage));
            }
            
            return Ok(false);
        }


        /// <summary>
        /// Check if a specific review version has been reviewed by Copilot
        /// </summary>
        /// <param name="reviewId">The ID of the review.</param>
        /// <param name="packageVersion">The package version to check for Copilot review.</param>
        [HttpGet("{reviewId}/isReviewVersionReviewedByCopilot")]
        public async Task<ActionResult<bool>> GetIsReviewVersionReviewedByCopilot(string reviewId, [FromQuery] string packageVersion)
        {
            IEnumerable<APIRevisionListItemModel> apiRevisions = await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId);

            bool isReviewed = apiRevisions.Any(revision => revision.PackageVersion == packageVersion && revision.HasAutoGeneratedComments);

            return Ok(isReviewed);
        }

        /// <summary>
        /// Get the count of revisions for a review
        /// </summary>
        /// <param name="reviewId">The ID of the review.</param>
        [HttpGet("{reviewId}/revisionCount")]
        public async Task<ActionResult<int>> GetReviewRevisionCount(string reviewId)
        {
            IEnumerable<APIRevisionListItemModel> apiRevisions = await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId, "", APIRevisionType.All);
            int count = apiRevisions.Count();
            return Ok(count);
        }

        /// <summary>
        /// Soft delete an entire review and all associated revisions (Admin only)
        /// </summary>
        /// <param name="reviewId">The ID of the review to delete</param>
        /// <returns></returns>
        [HttpDelete("{reviewId}")]
        public async Task<ActionResult> DeleteReviewAsync(string reviewId)
        {
            try
            {
                string userName = User.GetGitHubLogin();
                bool isAdmin = await _permissionsManager.IsAdminAsync(userName);
                
                if (!isAdmin)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can delete reviews.");
                }

                await _reviewManager.SoftDeleteReviewAsync(User, reviewId, skipOwnerCheck: true);
                return Ok();
            }
            catch (AuthorizationFailedException)
            {
                _logger.LogWarning("User not authorized to delete review {ReviewId}", reviewId);
                return StatusCode(StatusCodes.Status403Forbidden, "You are not authorized to delete this review.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting review {ReviewId}", reviewId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the review.");
            }
        }
    }
}
