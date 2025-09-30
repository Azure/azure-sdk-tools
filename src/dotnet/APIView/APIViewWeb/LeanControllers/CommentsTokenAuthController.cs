using System;
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
using Microsoft.Extensions.Logging;

namespace APIViewWeb.LeanControllers;

[ApiController]
[Authorize("RequireTokenAuthentication")]
[Route("api/comments")]
public class CommentsTokenAuthController : ControllerBase
{
    private readonly IAPIRevisionsManager _apiRevisionsManager;
    private readonly IBlobCodeFileRepository _codeFileRepository;
    private readonly ICommentsManager _commentsManager;
    private readonly ICosmosReviewRepository _cosmosReviewRepository;
    private readonly ILogger<CommentsTokenAuthController> _logger;

    public CommentsTokenAuthController(ICommentsManager commentsManager,
        IAPIRevisionsManager apiRevisionsManager,
        ICosmosReviewRepository cosmosReviewRepository,
        IBlobCodeFileRepository codeFileRepository,
        ILogger<CommentsTokenAuthController> logger)
    {
        _commentsManager = commentsManager;
        _apiRevisionsManager = apiRevisionsManager;
        _cosmosReviewRepository = cosmosReviewRepository;
        _codeFileRepository = codeFileRepository;
        _logger = logger;
    }

    /// <summary>
    /// Retrieve visible comments for a specific API revision.
    /// </summary>
    /// <param name="apiRevisionId">The unique identifier of the API revision</param>
    /// <returns>A list of comments formatted for agent consumption, or error result if revision not found</returns>
    [HttpGet("getRevisionComments", Name = "getRevisionComments")]
    public async Task<ActionResult<List<ApiViewAgentComment>>> GetRevisionComments([FromQuery] [Required] string apiRevisionId)
    {
        try
        {
            APIRevisionListItemModel apiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(apiRevisionId);

            if (apiRevision == null)
            {
                return new LeanJsonResult($"No revision found for id {apiRevisionId}", StatusCodes.Status404NotFound);
            }

            return await GetRevisionComments(apiRevision);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving revision comments for API revision ID: {ApiRevisionId}", apiRevisionId);
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve revision comments");
        }
    }


    /// <summary>
    /// Retrieve visible comments for the latest revision of a package by searching with package name and language.
    /// </summary>
    /// <param name="packageName">The name of the package</param>
    /// <param name="language">The programming language of the package</param>
    /// <param name="version">Optional specific version of the package</param>
    /// <returns>A list of comments formatted for agent consumption, or error result if package/revision not found</returns>
    [HttpGet("getCommentsByPackage", Name = "getCommentsByPackage")]
    public async Task<ActionResult<List<ApiViewAgentComment>>> GetCommentsByPackage(
        [FromQuery, Required] string packageName,
        [FromQuery, Required] string language,
        [FromQuery] string version = "")
    {
        try
        {
            (ReviewListItemModel _, APIRevisionListItemModel revision, ActionResult errorResult) = await ControllerHelpers.GetReviewAndRevisionAsync(
                _cosmosReviewRepository, _apiRevisionsManager, packageName, language, version);

            if (errorResult != null)
            {
                return errorResult;
            }

            return await GetRevisionComments(revision);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving revision comments for package: {PackageName}, language: {Language}, version: {Version}", 
                packageName, language, version);
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve revision comments by package");
        }
    }

    private async Task<ActionResult<List<ApiViewAgentComment>>> GetRevisionComments(APIRevisionListItemModel apiRevision)
    {
        RenderedCodeFile codeFile = await _codeFileRepository.GetCodeFileAsync(apiRevision, false);
        IEnumerable<CommentItemModel> comments =
            await _commentsManager.GetCommentsAsync(apiRevision.ReviewId, false, CommentType.APIRevision);

        List<ApiViewAgentComment> commentsForAgent = AgentHelpers.BuildCommentsForAgent(comments, codeFile);
        return new LeanJsonResult(commentsForAgent, StatusCodes.Status200OK);
    }
}
