using APIViewWeb.Extensions;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.LeanControllers
{
    public class ReviewsController : BaseApiController
    {
        private readonly ILogger<ReviewsController> _logger;
        private readonly IReviewManager _reviewManager;

        public ReviewsController(ILogger<ReviewsController> logger, IReviewManager reviewManager)
        {
            _logger = logger;
            _reviewManager = reviewManager;
        }

        /// <summary>
        /// Endpoint used by Client SPA for listing reviews. Uses a lean model that does not include full revision details
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        [HttpPost(Name = "GetReviews")]
        public async Task<ActionResult<PagedList<ReviewsListItemModel>>> GetReviewsAsync([FromQuery] PageParams pageParams, [FromBody] ReviewFilterParams filterParams)
        {
            var result = await _reviewManager.GetReviewsAsync(pageParams);
            Response.AddPaginationHeader(new PaginationHeader(result.NoOfItemsRead, result.PageSize, result.TotalCount));
            return new LeanJsonResult(result);
        }
    }
}
