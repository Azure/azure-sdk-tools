using APIViewWeb.Extensions;
using APIViewWeb.Helpers;
using APIViewWeb.Managers;
using APIViewWeb.Models;
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
        [HttpGet(Name = "GetReviews")]
        public async Task<ActionResult<PagedList<ReviewsListItemModel>>> GetReviewsAsync([FromQuery] PageParams pageParams)
        {
            var result = await _reviewManager.GetReviewsAsync(pageParams);
            Response.AddPaginationHeader(new PaginationHeader(result.CurrentPage, result.PageSize, result.TotalCount, result.TotalPages));
            return new LeanJsonResult(result);
        }
    }
}
