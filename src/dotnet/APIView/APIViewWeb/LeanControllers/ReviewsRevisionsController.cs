using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Extensions;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace APIViewWeb.LeanControllers
{
    public class ReviewRevisionsController : BaseApiController
    {
        private readonly ILogger<ReviewRevisionsController> _logger;
        private readonly IReviewRevisionsManager _reviewRevisionsManager;
        
        public ReviewRevisionsController(ILogger<ReviewRevisionsController> logger,
            IReviewRevisionsManager reviewRevisionsManager)
        {
            _logger = logger;
            _reviewRevisionsManager = reviewRevisionsManager;
        }

        /// <summary>
        /// Endpoint used by Client SPA for listing reviews. Uses a lean model that does not include full revision details
        /// </summary>
        /// <param name="pageParams"></param>
        /// <param name="filterAndSortParams"></param>
        /// <returns></returns>
        [HttpPost(Name = "GetReviewsRevisions")]
        public async Task<ActionResult<PagedList<ReviewListItemModel>>> GetReviewRevisionsAsync([FromQuery] PageParams pageParams, [FromBody] ReviewRevisionsFilterAndSortParams filterAndSortParams)
        {
            var result = await _reviewRevisionsManager.GetReviewRevisionsAsync(pageParams, filterAndSortParams);
            Response.AddPaginationHeader(new PaginationHeader(result.NoOfItemsRead, result.PageSize, result.TotalCount));
            return new LeanJsonResult(result, StatusCodes.Status200OK);
        }

    }
}
