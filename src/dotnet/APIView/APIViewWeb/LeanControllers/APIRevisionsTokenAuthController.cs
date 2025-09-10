using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.LeanControllers;

[ApiController]
[Authorize("RequireTokenAuthentication")]
[Route("api/apirevisions")]
public class APIRevisionsTokenAuthController : ControllerBase
{
    private readonly IAPIRevisionsManager _apiRevisionsManager;

    private readonly ILogger<APIRevisionsTokenAuthController> _logger;

    public APIRevisionsTokenAuthController(ILogger<APIRevisionsTokenAuthController> logger,
        IAPIRevisionsManager apiRevisionsManager)
    {
        _logger = logger;
        _apiRevisionsManager = apiRevisionsManager;
    }

    /// <summary>
    ///     Get the ApiRevision Outline for a given API Revision ID.
    /// </summary>
    /// <param name="apiRevisionId">The API revision ID to get the outline for</param>
    /// <returns>Revision outline text</returns>
    [HttpGet("{apiRevisionId}/outline", Name = "GetOutlineRevision")]
    public async Task<ActionResult<APIRevisionListItemModel>> GetOutlineAPIRevisionAsync(string apiRevisionId)
    {
        try
        {
            string revisionOutline = await _apiRevisionsManager.GetOutlineAPIRevisionsAsync(apiRevisionId);
            return new LeanJsonResult(revisionOutline, StatusCodes.Status200OK);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting outline review for revision {RevisionId}", apiRevisionId);
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to generate review text");
        }
    }

    /// <summary>
    ///     Generate review text for an API revision
    /// </summary>
    /// <param name="reviewId">The review ID</param>
    /// <param name="apiRevisionId">The specific API revision ID (required when selectionType is Specific)</param>
    /// <param name="selectionType">How to select the API revision</param>
    /// <returns>Plain text representation of the API review</returns>
    [HttpGet("getRevisionText", Name = "GetAPIRevisionText")]
    public async Task<ActionResult<string>> GetAPIRevisionTextAsync(
        [FromQuery] string apiRevisionId = null,
        [FromQuery] string reviewId = null,
        [FromQuery] APIRevisionSelectionType selectionType = APIRevisionSelectionType.Specific)
    {
        try
        {
            if (selectionType == APIRevisionSelectionType.Specific && string.IsNullOrEmpty(apiRevisionId))
            {
                return BadRequest("apiRevisionId is required when selectionType is Specific");
            }

            if (selectionType != APIRevisionSelectionType.Specific && string.IsNullOrEmpty(reviewId))
            {
                return BadRequest($"reviewId is required when selectionType is {selectionType}");
            }

            APIRevisionListItemModel activeApiRevision = null;
            switch (selectionType)
            {
                case APIRevisionSelectionType.Specific:
                    activeApiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(User, apiRevisionId);
                    break;
                case APIRevisionSelectionType.Latest:
                    activeApiRevision = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(reviewId);
                    break;
                case APIRevisionSelectionType.LatestApproved:
                    IEnumerable<APIRevisionListItemModel> allRevisions =
                        await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId);
                    activeApiRevision = allRevisions
                        .Where(r => r.IsApproved && !r.IsDeleted)
                        .OrderByDescending(r => r.CreatedOn)
                        .FirstOrDefault();
                    break;
                case APIRevisionSelectionType.LatestAutomatic:
                    activeApiRevision = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(
                        reviewId,
                        apiRevisionType: APIRevisionType.Automatic);
                    break;
                case APIRevisionSelectionType.LatestManual:
                    activeApiRevision = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(
                        reviewId,
                        apiRevisionType: APIRevisionType.Manual);
                    break;

                default:
                    return BadRequest($"Unsupported selection type: {selectionType}");
            }

            if (activeApiRevision == null)
            {
                return NotFound($"No API revision found for selection type: {selectionType}");
            }

            if (activeApiRevision.IsDeleted)
            {
                return new LeanJsonResult(null, StatusCodes.Status204NoContent);
            }

            if ((selectionType == APIRevisionSelectionType.Specific && !string.IsNullOrEmpty(reviewId) && activeApiRevision.ReviewId != reviewId) || 
                (selectionType != APIRevisionSelectionType.Specific && !string.IsNullOrEmpty(apiRevisionId) && activeApiRevision.Id != apiRevisionId))
            {
                return BadRequest(
                    $"Mismatch between reviewId and apiRevisionId: The API revision '{apiRevisionId}' does not belong to review '{reviewId}'. Ensure the revision ID corresponds to the specified review.");
            }

            string reviewText = await _apiRevisionsManager.GetApiRevisionText(activeApiRevision);
            return new LeanJsonResult(reviewText, StatusCodes.Status200OK);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error generating review text for revision selection {SelectionType} in review {ReviewId}",
                selectionType, reviewId);
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to generate review text");
        }
    }
}
