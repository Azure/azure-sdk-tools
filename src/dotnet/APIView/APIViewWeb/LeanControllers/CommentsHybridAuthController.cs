using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.LeanControllers;

[ApiController]
[Authorize("RequireTokenOrCookieAuthentication")]
[Route("api/comments")]
public class CommentsHybridAuthController : ControllerBase
{
    private readonly ICommentsManager _commentsManager;

    public CommentsHybridAuthController(ICommentsManager commentsManager)
    {
        _commentsManager = commentsManager;
    }

    /// <summary>
    ///     Retrieve comments for a review.
    /// </summary>
    /// <param name="reviewId"></param>
    /// <param name="isDeleted"></param>
    /// <param name="commentType"></param>
    /// <returns></returns>
    [HttpGet("{reviewId}", Name = "GetComments")]
    public async Task<ActionResult<IEnumerable<CommentItemModel>>> GetCommentsAsync(string reviewId,
        bool isDeleted = false, CommentType? commentType = null)
    {
        IEnumerable<CommentItemModel> comments =
            await _commentsManager.GetCommentsAsync(reviewId, isDeleted, commentType);
        return new LeanJsonResult(comments, StatusCodes.Status200OK);
    }
}
