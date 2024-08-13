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
using Microsoft.Extensions.Hosting;
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
        public readonly UserPreferenceCache _preferenceCache;
        private readonly ICosmosUserProfileRepository _userProfileRepository;
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly IWebHostEnvironment _env;

        public ReviewsController(ILogger<ReviewsController> logger,
            IAPIRevisionsManager reviewRevisionsManager, IReviewManager reviewManager,
            ICommentsManager commentManager, IBlobCodeFileRepository codeFileRepository,
            IConfiguration configuration, UserPreferenceCache preferenceCache,
            ICosmosUserProfileRepository userProfileRepository, IHubContext<SignalRHub> signalRHub, IWebHostEnvironment env)
        {
            _logger = logger;
            _apiRevisionsManager = reviewRevisionsManager;
            _reviewManager = reviewManager;
            _commentsManager = commentManager;
            _codeFileRepository = codeFileRepository;
            _configuration = configuration;
            _preferenceCache = preferenceCache;
            _userProfileRepository = userProfileRepository;
            _signalRHubContext = signalRHub;
            _env = env;

        }

        /// <summary>
        /// Retrieves a list of reviews grouped by pages
        /// </summary>
        /// <param name="pageParams"></param>
        /// <param name="filterAndSortParams"></param>
        /// <returns></returns>
        [HttpGet(Name = "GetReviews")]
        public async Task<ActionResult<PagedList<ReviewListItemModel>>> GetReviewsAsync([FromQuery] PageParams pageParams, [FromQuery] ReviewFilterAndSortParams filterAndSortParams)
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

        [HttpGet("{reviewId}/preferredApprovers", Name = "PreferredApprovers")]
        public async Task<ActionResult<HashSet<string>>> GetPreferredApproversAsync(string reviewId)
        {
            var review = await _reviewManager.GetReviewAsync(User, reviewId);
            HashSet<string> preferredApprovers = await PageModelHelpers.GetPreferredApprovers(_configuration, _preferenceCache, _userProfileRepository, User, review);
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
            return new LeanJsonResult(updatedReview, StatusCodes.Status200OK);
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
        public async Task<ActionResult<CodePanelData>> GetReviewContentAsync(string reviewId, [FromQuery] string activeApiRevisionId = null,
            [FromQuery] string diffApiRevisionId = null)
        {
            var activeAPIRevision = await _apiRevisionsManager.GetAPIRevisionAsync(User, activeApiRevisionId);

            if (activeAPIRevision.Files[0].ParserStyle == ParserStyle.Tree)
            {
                var comments = await _commentsManager.GetCommentsAsync(reviewId);

                var activeRevisionReviewCodeFile = await _codeFileRepository.GetCodeFileWithCompressionAsync(revisionId: activeAPIRevision.Id, codeFileId: activeAPIRevision.Files[0].FileId); 

                var result = new CodePanelData();

                var codePanelRawData = new CodePanelRawData()
                {
                    APIForest = activeRevisionReviewCodeFile.APIForest,
                    Diagnostics = activeRevisionReviewCodeFile.Diagnostics,
                    Comments = comments,
                    Language = activeRevisionReviewCodeFile.Language
                };

                if (!string.IsNullOrEmpty(diffApiRevisionId))
                {
                    var diffAPIRevision = await _apiRevisionsManager.GetAPIRevisionAsync(User, diffApiRevisionId);

                    var diffRevisionReviewCodeFile = await _codeFileRepository.GetCodeFileWithCompressionAsync(revisionId: diffAPIRevision.Id, codeFileId: diffAPIRevision.Files[0].FileId);
                    codePanelRawData.APIForest = CodeFileHelpers.ComputeAPIForestDiff(diffRevisionReviewCodeFile.APIForest, activeRevisionReviewCodeFile.APIForest);
                }

                result = await CodeFileHelpers.GenerateCodePanelDataAsync(codePanelRawData);
                return new LeanJsonResult(result, StatusCodes.Status200OK);
            }

            return new LeanJsonResult("Invalid APIRevision", StatusCodes.Status500InternalServerError);
        }
    }
}
