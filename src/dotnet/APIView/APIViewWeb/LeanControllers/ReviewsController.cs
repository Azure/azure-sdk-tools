using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Extensions;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Hosting;
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
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        private readonly IConfiguration _configuration;
        public readonly UserProfileCache _userProfileCache;
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly INotificationManager _notificationManager;

        public ReviewsController(ILogger<ReviewsController> logger,
            IAPIRevisionsManager reviewRevisionsManager, IReviewManager reviewManager,
            ICommentsManager commentManager, IBlobCodeFileRepository codeFileRepository,
            IConfiguration configuration, UserProfileCache userProfileCache,
            IEnumerable<LanguageService> languageServices,
            IHubContext<SignalRHub> signalRHub, INotificationManager notificationManager,
            IWebHostEnvironment env)
        {
            _apiRevisionsManager = reviewRevisionsManager;
            _reviewManager = reviewManager;
            _codeFileRepository = codeFileRepository;
            _configuration = configuration;
            _userProfileCache = userProfileCache;
            _signalRHubContext = signalRHub;
            _notificationManager = notificationManager;
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

        [HttpGet("allowedApprovers", Name = "AllowedApprovers")]
        public ActionResult<HashSet<string>> GetAllowedApproversAsync()
        {
            var allowedApprovers = _configuration["approvers"];
            return new LeanJsonResult(allowedApprovers, StatusCodes.Status200OK);
        }

        [HttpGet("{reviewId}/preferredApprovers", Name = "PreferredApprovers")]
        public async Task<ActionResult<HashSet<string>>> GetPreferredApproversAsync(string reviewId)
        {
            var review = await _reviewManager.GetReviewAsync(User, reviewId);
            HashSet<string> preferredApprovers = PageModelHelpers.GetPreferredApprovers(_configuration, _userProfileCache, User, review);
            return new LeanJsonResult(preferredApprovers, StatusCodes.Status200OK);
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
    }
}
