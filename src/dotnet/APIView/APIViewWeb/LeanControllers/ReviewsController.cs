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
using System.Linq;
using System;
using APIView;
using Microsoft.AspNetCore.Authorization;
using APIViewWeb.Pages.Assemblies;

namespace APIViewWeb.LeanControllers
{
    public class ReviewsController : BaseApiController
    {
        private readonly ILogger<ReviewsController> _logger;
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly ICommentsManager _commentManager;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        
        public ReviewsController(ILogger<ReviewsController> logger,
            IAPIRevisionsManager reviewRevisionsManager, IReviewManager reviewManager,
            ICommentsManager commentManager, IBlobCodeFileRepository codeFileRepository)
        {
            _logger = logger;
            _apiRevisionsManager = reviewRevisionsManager;
            _reviewManager = reviewManager;
            _commentManager = commentManager;
            _codeFileRepository = codeFileRepository;
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
        ///<param name="revisionId"></param>
        ///<returns></returns>
        [HttpGet]
        [Route("{reviewId}/content")]
        public async Task<ActionResult<ReviewContentModel>> GetReviewContentAsync(string reviewId, [FromQuery]string revisionId=null)
        {
           var review = await _reviewManager.GetReviewAsync(user:User, id: reviewId);
           var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId);
           var activeRevision = (string.IsNullOrEmpty(revisionId)) ? 
               await _apiRevisionsManager.GetLatestAPIRevisionsAsync(reviewId, revisions) : await _apiRevisionsManager.GetAPIRevisionAsync(user: User, apiRevisionId: revisionId);
           var comments = await _commentManager.GetReviewCommentsAsync(reviewId);

           var renderableCodeFile = await _codeFileRepository.GetCodeFileAsync(activeRevision.Id, activeRevision.Files[0].FileId);
           var reviewCodeFile = renderableCodeFile.CodeFile;
           var fileDiagnostics = reviewCodeFile.Diagnostics ?? Array.Empty<CodeDiagnostic>();
           var htmlLines = renderableCodeFile.Render(showDocumentation: false);
           var codeLines = PageModelHelpers.CreateLines(diagnostics: fileDiagnostics, lines: htmlLines, comments: comments);

           var pageModel = new ReviewContentModel
           {
               Review = review,
               Navigation = renderableCodeFile.CodeFile.Navigation,
               codeLines = codeLines,
               APIRevisionsGrouped = revisions.GroupBy(r => r.APIRevisionType).ToDictionary(r => r.Key.ToString(), r => r.ToList()),
               ActiveAPIRevision = activeRevision
           };

           return new LeanJsonResult(pageModel, StatusCodes.Status200OK);
        }
    }
}
