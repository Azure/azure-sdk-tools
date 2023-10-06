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

namespace APIViewWeb.LeanControllers
{
    public class ReviewsController : BaseApiController
    {
        private readonly ILogger<ReviewsController> _logger;
        private readonly IReviewManager _reviewManager;
        private readonly IReviewRevisionsManager _reviewRevisionsManager;
        private readonly ICommentsManager _commentManager;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        
        public ReviewsController(ILogger<ReviewsController> logger,
            IReviewRevisionsManager reviewRevisionsManager, IReviewManager reviewManager,
            ICommentsManager commentManager, IBlobCodeFileRepository codeFileRepository)
        {
            _logger = logger;
            _reviewRevisionsManager = reviewRevisionsManager;
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
        [HttpPost(Name = "GetReviews")]
        public async Task<ActionResult<PagedList<ReviewListItemModel>>> GetReviewsAsync([FromQuery] PageParams pageParams, [FromBody] ReviewFilterAndSortParams filterAndSortParams)
        {
            var result = await _reviewManager.GetReviewsAsync(pageParams, filterAndSortParams);
            Response.AddPaginationHeader(new PaginationHeader(result.NoOfItemsRead, result.PageSize, result.TotalCount));
            return new LeanJsonResult(result, StatusCodes.Status200OK);
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
            var review = await _reviewManager.GetReviewAsync(reviewId);
            var revisions = await _reviewRevisionsManager.GetReviewRevisionsAsync(reviewId);
            var activeRevision = (string.IsNullOrEmpty(revisionId)) ? 
                await _reviewRevisionsManager.GetLatestReviewRevisionsAsync(reviewId, revisions) : await _reviewRevisionsManager.GetReviewRevisionAsync(revisionId);
            var comments = await _commentManager.GetReviewCommentsAsync(reviewId);

            var renderableCodeFile = await _codeFileRepository.GetCodeFileAsync(activeRevision.Id, activeRevision.Files[0].ReviewFileId);
            var reviewCodeFile = renderableCodeFile.CodeFile;
            var fileDiagnostics = reviewCodeFile.Diagnostics ?? Array.Empty<CodeDiagnostic>();
            var htmlLines = renderableCodeFile.Render(showDocumentation: false);
            var codeLines = PageModelHelpers.CreateLines(diagnostics: fileDiagnostics, lines: htmlLines, comments: comments);

            var pageModel = new ReviewContentModel
            {
                Review = review,
                Navigation = renderableCodeFile.CodeFile.Navigation,
                codeLines = codeLines,
                ReviewRevisions = revisions.GroupBy(r => r.ReviewRevisionType).ToDictionary(r => r.Key.ToString(), r => r.ToList()),
                ActiveRevision = activeRevision
            };

            return new LeanJsonResult(pageModel, StatusCodes.Status200OK);
         }
    }
}
