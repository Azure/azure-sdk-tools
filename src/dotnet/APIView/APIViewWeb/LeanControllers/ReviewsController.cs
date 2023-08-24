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

        [HttpGet]
        public async Task<ActionResult> GetReviewsAsync()
        {
            return Ok();
        }
    }
}
