using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
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
[Route("api/apirevisions")]
public class APIRevisionsTokenAuthController : ControllerBase
{
    private readonly IAPIRevisionsManager _apiRevisionsManager;
    private readonly IBlobCodeFileRepository _codeFileRepository;
    private readonly ICosmosReviewRepository _cosmosReviewRepository;
    private readonly ILogger<APIRevisionsTokenAuthController> _logger;

    public APIRevisionsTokenAuthController(IBlobCodeFileRepository codeFileRepository,
        IAPIRevisionsManager apiRevisionsManager,
        ICosmosReviewRepository cosmosReviewRepository,
        ILogger<APIRevisionsTokenAuthController> logger)
    {
        _apiRevisionsManager = apiRevisionsManager;
        _codeFileRepository = codeFileRepository;
        _cosmosReviewRepository = cosmosReviewRepository;
        _logger = logger;
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
    /// <param name="apiRevisionId">The specific API revision ID (required when there is not selectionType)</param>
    /// <param name="selectionType">How to select the API revision</param>
    /// <param name="contentReturnType">The content return type. Default is text, but CodeFile can also be selected</param>
    /// <returns>Plain text representation of the API review</returns>
    [HttpGet("getRevisionContent", Name = "GetAPIRevisionContent")]
    public async Task<ActionResult> GetAPIRevisionContentAsync(
        [FromQuery] string apiRevisionId = null,
        [FromQuery] string reviewId = null,
        [FromQuery] APIRevisionSelectionType selectionType = APIRevisionSelectionType.Undefined,
        [FromQuery] APIRevisionContentReturnType contentReturnType = APIRevisionContentReturnType.Text)
    {
        return await GetContentInternalAsync(apiRevisionId, reviewId, selectionType, contentReturnType);
    }

    /// <summary>
    ///     Generate review tex or code file for a package by package name, language, and version
    /// </summary>
    /// <param name="packageName">The package name</param>
    /// <param name="language">The programming language</param>
    /// <param name="version">The package version (optional, defaults to latest)</param>
    /// <param name="selectionType">How to select the API revision</param>
    /// <param name="contentReturnType">The content return type. Default is text, but CodeFile can also be selected</param>
    /// <returns>Plain text representation of the API review</returns>
    [HttpGet("getPackageContent", Name = "GetPackageContent")]
    public async Task<ActionResult> GetPackageContent(
        [FromQuery, Required] string packageName,
        [FromQuery, Required] string language,
        [FromQuery] string version = "",
        [FromQuery] APIRevisionSelectionType selectionType = APIRevisionSelectionType.Undefined,
        [FromQuery] APIRevisionContentReturnType contentReturnType = APIRevisionContentReturnType.Text)
    {
        try
        {
            (ReviewListItemModel review, APIRevisionListItemModel revision, ActionResult errorResult) = await ControllerHelpers.GetReviewAndRevisionAsync(
                _cosmosReviewRepository, _apiRevisionsManager, packageName, language, version);

            if (errorResult != null)
            {
                return errorResult;
            }

            if (selectionType != APIRevisionSelectionType.Undefined)
            {
                return await GetContentInternalAsync(string.Empty, review.Id, selectionType, contentReturnType);
            }

            return await GetContentInternalAsync(revision.Id, string.Empty, selectionType, contentReturnType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting package content for {PackageName} in {Language} version {Version}",
                packageName, language, version);
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to generate package content");
        }
    }

    private async Task<ActionResult> GetContentInternalAsync(
        string apiRevisionId,
        string reviewId,
        APIRevisionSelectionType selectionType,
        APIRevisionContentReturnType contentReturnType)
    {
        try
        {
            if (selectionType == APIRevisionSelectionType.Undefined && string.IsNullOrEmpty(apiRevisionId))
            {
                return new LeanJsonResult("The apiRevisionId parameter is required when there is not a selection type", StatusCodes.Status400BadRequest);
            }

            if (selectionType != APIRevisionSelectionType.Undefined && string.IsNullOrEmpty(reviewId))
            {
                return new LeanJsonResult($"The reviewId parameter is required when selectionType is {selectionType}", StatusCodes.Status400BadRequest);
            }

            APIRevisionListItemModel activeApiRevision = await GetApiRevisionBySelectionType(selectionType, reviewId, apiRevisionId);
            if (activeApiRevision == null || activeApiRevision.IsDeleted)
            {
                return new LeanJsonResult($"No API revision found for selection type: {selectionType}", StatusCodes.Status404NotFound);
            }

            if (IsValidateRevisionMatch(activeApiRevision, reviewId, apiRevisionId, selectionType))
            {
                return new LeanJsonResult(
                    $"Mismatch between reviewId and apiRevisionId: The API revision '{apiRevisionId}' does not belong to review '{reviewId}'. Ensure the revision ID corresponds to the specified review.",
                    StatusCodes.Status400BadRequest);
            }

            switch (contentReturnType)
            {
                case APIRevisionContentReturnType.Text:
                    string reviewText = await _apiRevisionsManager.GetApiRevisionText(activeApiRevision);
                    return new LeanJsonResult(reviewText, StatusCodes.Status200OK);
                case APIRevisionContentReturnType.CodeFile:
                    if (activeApiRevision.Files == null || !activeApiRevision.Files.Any())
                    {
                        return new LeanJsonResult($"No files found for API revision ID: {activeApiRevision.Id}", StatusCodes.Status404NotFound);
                    }
                    
                    CodeFile activeRevisionReviewCodeFile = await _codeFileRepository.GetCodeFileFromStorageAsync(activeApiRevision.Id, activeApiRevision.Files[0].FileId);
                    if (activeRevisionReviewCodeFile == null)
                    {
                        return new LeanJsonResult($"No code file found for API revision ID: {activeApiRevision.Id}", StatusCodes.Status404NotFound);
                    }

                    return new LeanJsonResult(activeRevisionReviewCodeFile, StatusCodes.Status200OK);
                default:
                    return new LeanJsonResult(
                        $"Unsupported contentReturnType: {contentReturnType}. Supported filters are {APIRevisionContentReturnType.Text} and {APIRevisionContentReturnType.CodeFile}",
                        StatusCodes.Status400BadRequest);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error generating review text for revision selection {SelectionType} in review {ReviewId}",
                selectionType, reviewId);
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to generate review text");
        }
    }

    private async Task<APIRevisionListItemModel> GetApiRevisionBySelectionType(
        APIRevisionSelectionType selectionType, string reviewId, string apiRevisionId)
    {
        return selectionType switch
        {
            APIRevisionSelectionType.Undefined => await _apiRevisionsManager.GetAPIRevisionAsync(User, apiRevisionId),
            APIRevisionSelectionType.Latest => await _apiRevisionsManager.GetLatestAPIRevisionsAsync(reviewId),
            APIRevisionSelectionType.LatestApproved => await GetLatestApprovedRevision(reviewId),
            APIRevisionSelectionType.LatestAutomatic => await _apiRevisionsManager.GetLatestAPIRevisionsAsync(reviewId, apiRevisionType: APIRevisionType.Automatic),
            APIRevisionSelectionType.LatestManual => await _apiRevisionsManager.GetLatestAPIRevisionsAsync(reviewId, apiRevisionType: APIRevisionType.Manual),
            _ => throw new ArgumentException($"Unsupported selection type: {selectionType}")
        };
    }

    private async Task<APIRevisionListItemModel> GetLatestApprovedRevision(string reviewId)
    {
        IEnumerable<APIRevisionListItemModel> allRevisions = await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId);
        return allRevisions
            .Where(r => r.IsApproved && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedOn)
            .FirstOrDefault();
    }

    private bool IsValidateRevisionMatch(APIRevisionListItemModel activeApiRevision, string reviewId, string apiRevisionId, APIRevisionSelectionType selectionType)
    {
        bool hasReviewIdMismatch = selectionType == APIRevisionSelectionType.Undefined &&
                                   !string.IsNullOrEmpty(reviewId) && activeApiRevision.ReviewId != reviewId;

        bool hasRevisionIdMismatch = selectionType != APIRevisionSelectionType.Undefined &&
                                     !string.IsNullOrEmpty(apiRevisionId) && activeApiRevision.Id != apiRevisionId;

        return hasRevisionIdMismatch || hasReviewIdMismatch;
    }
}
