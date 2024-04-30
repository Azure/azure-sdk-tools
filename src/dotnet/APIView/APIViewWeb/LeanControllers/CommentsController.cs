using APIViewWeb.Extensions;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIViewWeb.LeanControllers
{
    public class CommentsController : BaseApiController
    {
        private readonly ILogger<CommentsController> _logger;
        private readonly ICommentsManager _commentsManager;

        public CommentsController(ILogger<CommentsController> logger, ICommentsManager commentManager)
        {
            _logger = logger;
            _commentsManager = commentManager;
        }

        /// <summary>
        /// Retrieve comments for a review.
        /// </summary>
        /// <param name="reviewId"></param>
        /// <returns></returns>
        [HttpGet("/{reviewId}", Name = "GetComments")]
        public async Task<ActionResult<IEnumerable<CommentItemModel>>> GetCommentsAsync(string reviewId)
        {
            var comments = await _commentsManager.GetCommentsAsync(reviewId);
            return new LeanJsonResult(comments, StatusCodes.Status200OK);
        }
    }
}
