using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace APIViewWeb.LeanControllers
{
    public class ReviewsController : BaseApiController
    {
        private readonly ILogger<ReviewsController> _logger;
        private readonly IReviewManager _reviewManager;
        private readonly IRevisionManager _revisionsManager;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        
        public ReviewsController(ILogger<ReviewsController> logger,
            IRevisionManager revisionsManager, IReviewManager reviewManager, IBlobCodeFileRepository codeFileRepository)
        {
            _logger = logger;
            _revisionsManager = revisionsManager;
            _reviewManager = reviewManager;
            _codeFileRepository = codeFileRepository;
        }

        /// <summary>
        /// Endpoint used by Client SPA for listing reviews. Uses a lean model that does not include full revision details
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        [HttpPost(Name = "GetReviews")]
        public async Task<ActionResult<PagedList<ReviewsListItemModel>>> GetReviewsAsync([FromQuery] PageParams pageParams, [FromBody] ReviewFilterAndSortParams filterAndSortParams)
        {
            var result = await _reviewManager.GetReviewsAsync(pageParams, filterAndSortParams);
            Response.AddPaginationHeader(new PaginationHeader(result.NoOfItemsRead, result.PageSize, result.TotalCount));
            return new LeanJsonResult(result);
        }


        [HttpGet(Name = "GetReviewPage")]
        [Route("{reviewId}")]
        public async Task<ActionResult<ReviewPageModel>> GetReviewPageAsync(string reviewId, string revisionId= null)
        {
            var revision = (string.IsNullOrEmpty(revisionId)) ? 
                await _revisionsManager.GetLatestRevisionsAsync(reviewId) : await _revisionsManager.GetRevisionsAsync(reviewId, revisionId);
            var reviewCodeFie = await _codeFileRepository.GetCodeFileAsync(revision.Id, revision.Files[0].ReviewFileId);
            reviewCodeFie.RenderText(showDocumentation: true);

            var pageModel = new ReviewPageModel
            {
                Navigation = reviewCodeFie.CodeFile.Navigation,
                codeLines = reviewCodeFie.RenderResultText.CodeLines
            };
            return new LeanJsonResult(pageModel);
        }
    }
}
