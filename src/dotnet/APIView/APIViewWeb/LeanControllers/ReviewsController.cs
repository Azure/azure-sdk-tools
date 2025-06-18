using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Extensions;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using APIViewWeb.Managers.Interfaces;
using Microsoft.Extensions.Configuration;
using APIViewWeb.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Generic;

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
        private readonly IWebHostEnvironment _env;

        public ReviewsController(ILogger<ReviewsController> logger,
            IAPIRevisionsManager reviewRevisionsManager, IReviewManager reviewManager,
            ICommentsManager commentManager, IBlobCodeFileRepository codeFileRepository,
            IConfiguration configuration, UserProfileCache userProfileCache,
            IEnumerable<LanguageService> languageServices,
            IHubContext<SignalRHub> signalRHub,
            INotificationManager notificationManager, IWebHostEnvironment env)
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
            _env = env;
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
        /// <returns></returns>
        [HttpPost("{reviewId}/{apiRevisionId}", Name = "ToggleReviewApproval")]
        public async Task<ActionResult> ToggleReviewApprovalAsync(string reviewId, string apiRevisionId)
        {
            var updatedReview = await _reviewManager.ToggleReviewApprovalAsync(User, reviewId, apiRevisionId);
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
                var comments = await _commentsManager.GetCommentsAsync(reviewId, commentType: CommentType.APIRevision);
                var activeRevisionReviewCodeFile = await _codeFileRepository.GetCodeFileFromStorageAsync(revisionId: activeAPIRevision.Id, codeFileId: activeAPIRevision.Files[0].FileId);

                if (activeRevisionReviewCodeFile.ContentGenerationInProgress)
                {
                    var languageServices = LanguageServiceHelpers.GetLanguageService(activeAPIRevision.Language, _languageServices);
                    return new LeanJsonResult("Content generation in progress", StatusCodes.Status202Accepted, languageServices.ReviewGenerationPipelineUrl);
                }

                var codePanelRawData = new CodePanelRawData()
                {
                    activeRevisionCodeFile = activeRevisionReviewCodeFile,
                    Comments = comments
                };

                if (diffAPIRevision != null)
                {
                    codePanelRawData.diffRevisionCodeFile = await _codeFileRepository.GetCodeFileFromStorageAsync(revisionId: diffAPIRevision.Id, codeFileId: diffAPIRevision.Files[0].FileId);
                }

                // Render the code files to generate UI token tree
                var result = await CodeFileHelpers.GenerateCodePanelDataAsync(codePanelRawData);
                return new LeanJsonResult(result, StatusCodes.Status200OK);
            }

            return new LeanJsonResult("Invalid APIRevision", StatusCodes.Status500InternalServerError);
        }
    }
}
