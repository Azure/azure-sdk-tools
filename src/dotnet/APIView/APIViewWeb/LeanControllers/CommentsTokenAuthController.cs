using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.LeanControllers;

[ApiController]
[Authorize("RequireTokenAuthentication")]
[Route("api/comments")]
public class CommentsTokenAuthController : ControllerBase
{
    private readonly ICommentsManager _commentsManager;
    private readonly IAPIRevisionsManager _apiRevisionsManager;
    private readonly IBlobCodeFileRepository _codeFileRepository;

    public CommentsTokenAuthController(ICommentsManager commentsManager,
        IAPIRevisionsManager apiRevisionsManager,
        IBlobCodeFileRepository codeFileRepository)
    {
        _commentsManager = commentsManager;
        _apiRevisionsManager = apiRevisionsManager;
        _codeFileRepository = codeFileRepository;
    }

    /// <summary>
    ///     Retrieve visible comments for a revision.
    /// </summary>
    /// <param name="apiRevisionId"></param>
    /// <returns></returns>
    [HttpGet("getRevisionComments", Name = "getRevisionComments")]
    public async Task<ActionResult<List<ApiViewAgentComment>>> GetRevisionComments([FromQuery, Required] string apiRevisionId)
    {
        APIRevisionListItemModel apiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(apiRevisionId);
        if (apiRevision == null)
        {
            return new LeanJsonResult("API revision not found", StatusCodes.Status404NotFound);
        }

        RenderedCodeFile codeFile = await _codeFileRepository.GetCodeFileAsync(apiRevision, false);
        IEnumerable<CommentItemModel> comments = await _commentsManager.GetCommentsAsync(apiRevision.ReviewId, isDeleted:false, CommentType.APIRevision);

        List<ApiViewAgentComment> commentsForAgent = AgentHelpers.BuildCommentsForAgent(comments, codeFile);
        return new LeanJsonResult(commentsForAgent, StatusCodes.Status200OK);
    }
}
