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

        public ReviewsController(ILogger<ReviewsController> logger,
            IAPIRevisionsManager reviewRevisionsManager, IReviewManager reviewManager,
            ICommentsManager commentManager, IBlobCodeFileRepository codeFileRepository,
            IConfiguration configuration, UserPreferenceCache preferenceCache,
            ICosmosUserProfileRepository userProfileRepository, IHubContext<SignalRHub> signalRHub)
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

        }

        /// <summary>
        /// Endpoint used by Client SPA for listing reviews.
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
        /// Endpoint used by Client SPA for creating reviews.
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

        ///<summary>
        ///Retrieve the Content (codeLines and Navigation) of a review
        ///</summary>
        ///<param name="reviewId"></param>
        ///<param name="activeApiRevisionId"></param>
        /// <param name="diffApiRevisionId"></param>
        ///<returns></returns>
        [HttpGet]
        [Route("{reviewId}/content")]
        public async Task<ActionResult<ReviewContentModel>> GetReviewContentAsync(string reviewId, [FromQuery] string activeApiRevisionId = null,
            [FromQuery] string diffApiRevisionId = null)
        {
            var result = new ReviewCodePanelData();
            var activeAPIRevision = await _apiRevisionsManager.GetAPIRevisionAsync(User, activeApiRevisionId);
            var comments = await _commentsManager.GetCommentsAsync(reviewId);

            var activeRevisionRenderableCodeFile = await _codeFileRepository.GetCodeFileAsync(activeAPIRevision.Id, activeAPIRevision.Files[0].FileId);
            var activeRevisionReviewCodeFile = activeRevisionRenderableCodeFile.CodeFile;

            if (activeRevisionReviewCodeFile.CodeFileVersion.Equals("v2")) 
            {
                result.APIForest = activeRevisionReviewCodeFile.APIForest;
                result.Diagnostics = activeRevisionReviewCodeFile.Diagnostics;
                result.Comments = comments;

                if (!string.IsNullOrEmpty(diffApiRevisionId))
                {
                    var diffAPIRevision = await _apiRevisionsManager.GetAPIRevisionAsync(User, diffApiRevisionId);
                    var diffRevisionRenderableCodeFile = await _codeFileRepository.GetCodeFileAsync(diffAPIRevision.Id, diffAPIRevision.Files[0].FileId);
                    result.APIForest = CodeFileHelpers.ComputeAPIForestDiff(activeRevisionReviewCodeFile.APIForest, diffRevisionRenderableCodeFile.CodeFile.APIForest);
                }
                return new LeanJsonResult(result, StatusCodes.Status200OK);
            }

            return new LeanJsonResult("Invalid APIRevision", StatusCodes.Status500InternalServerError);
        }
    }
}
