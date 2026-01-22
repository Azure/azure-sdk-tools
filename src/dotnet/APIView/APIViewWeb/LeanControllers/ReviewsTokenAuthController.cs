using System.Collections.Generic;
using System.Linq;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.LeanControllers;

[ApiController]
[Authorize("RequireTokenAuthentication")]
[Route("api/reviews")]
public class ReviewsTokenAuthController : ControllerBase
{
    private readonly IReviewSearch reviewSearch;
    private readonly IAPIRevisionsManager _apiRevisionsManager;
    private readonly ICosmosPullRequestsRepository _pullRequestsRepository;
    private readonly IReviewManager _reviewManager;
    private readonly IConfiguration _configuration;
    private readonly IEnumerable<LanguageService> _languageServices;
    private readonly ILogger<ReviewsTokenAuthController> _logger;

    public ReviewsTokenAuthController(
        IReviewSearch reviewSearch,
        IAPIRevisionsManager apiRevisionsManager,
        ICosmosPullRequestsRepository pullRequestsRepository,
        IReviewManager reviewManager,
        IConfiguration configuration,
        IEnumerable<LanguageService> languageServices,
        ILogger<ReviewsTokenAuthController> logger)
    {
        this.reviewSearch = reviewSearch;
        _apiRevisionsManager = apiRevisionsManager;
        _reviewManager = reviewManager;
        _pullRequestsRepository = pullRequestsRepository;
        _configuration = configuration;
        _languageServices = languageServices;
        _logger = logger;
    }

    /// <summary>
    /// Resolve a review/revision from various input types.
    /// Returns IDs that can be cached and used for subsequent API calls.
    /// 
    /// Supports:
    /// - Package name + language (+ optional version)
    /// - APIView URL (review or revision link)
    /// </summary>
    /// <param name="packageQuery">Package name (e.g., "Azure.Storage.Blobs"). Requires language.</param>
    /// <param name="language">Programming language (e.g., "C#", "Python"). Requires packageQuery.</param>
    /// <param name="version">Optional package version. If not specified, resolves to latest revision.</param>
    /// <param name="link">APIView URL (e.g., "https://spa.apiview.dev/review/{id}?activeApiRevisionId={revId}")</param>
    /// <returns>Resolved IDs for use in subsequent API calls.</returns>
    [HttpGet("resolve", Name = "ResolveReview")]
    public async Task<ActionResult<ResolvePackageResponse>> Resolve(
        [FromQuery] string packageQuery = null,
        [FromQuery] string language = null,
        [FromQuery] string version = null,
        [FromQuery] string link = null)
    {
        try
        {
            ResolvePackageResponse result;

            if (!string.IsNullOrEmpty(link))
            {
                result = await reviewSearch.ResolvePackageLink(link);
            }
            else if (!string.IsNullOrEmpty(packageQuery) && !string.IsNullOrEmpty(language))
            {
                result = await reviewSearch.ResolvePackageQuery(packageQuery, language, version);
            }
            else
            {
                return BadRequest(
                    "Invalid parameters. Provide one of: " +
                    "(1) 'link' (APIView URL), or " +
                    "(2) 'packageQuery' + 'language' (with optional 'version').");
            }

            if (result == null)
            {
                return NotFound("Could not find a review/revision matching the provided parameters.");
            }

            return new LeanJsonResult(result, StatusCodes.Status200OK);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error resolving review/revision");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while resolving.");
        }
    }

    /// <summary>
    /// Get full metadata for a revision.
    /// Requires a revisionId (obtained from the /resolve endpoint).
    /// </summary>
    /// <param name="revisionId">The API revision ID (from /resolve endpoint).</param>
    /// <returns>Full review and revision metadata.</returns>
    [HttpGet("metadata", Name = "GetReviewMetadata")]
    public async Task<ActionResult<ReviewMetadata>> GetMetadata([FromQuery] string revisionId)
    {
        if (string.IsNullOrEmpty(revisionId))
        {
            return BadRequest("'revisionId' is required. Use the /resolve endpoint first to obtain the revision ID.");
        }

        try
        {
            APIRevisionListItemModel revision = await _apiRevisionsManager.GetAPIRevisionAsync(revisionId);
            if (revision == null)
            {
                return NotFound($"Revision '{revisionId}' not found.");
            }

            ReviewListItemModel review = (await _reviewManager.GetReviewsAsync([revision.ReviewId])).FirstOrDefault();
            if (review == null)
            {
                return NotFound($"Review for revision '{revisionId}' not found.");
            }

            string pullRequestRepo = null;
            if (revision.PullRequestNo != null)
            {
                IEnumerable<PullRequestModel> pullRequests = await _pullRequestsRepository.GetPullRequestsAsync(revision.ReviewId, revision.Id);
                pullRequestRepo = pullRequests.FirstOrDefault()?.RepoName;
            }

            ReviewMetadata metadata = MapToMetadata(review, revision, pullRequestRepo);
            return new LeanJsonResult(metadata, StatusCodes.Status200OK);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metadata for revision {RevisionId}", revisionId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving metadata.");
        }
    }

    private ReviewMetadata MapToMetadata(ReviewListItemModel review, APIRevisionListItemModel revision, string pullRequestRepo)
    {
        var revisionLink = ManagerHelpers.ResolveReviewUrl(
            review.Id,
            revision.Id,
            review.Language,
            _configuration,
            _languageServices);

        return new ReviewMetadata
        {
            ReviewId = review.Id,
            PackageName = review.PackageName,
            Language = review.Language,
            IsApproved = review.IsApproved,
            CreatedBy = review.CreatedBy,
            CreatedOn = review.CreatedOn,
            LastUpdatedOn = review.LastUpdatedOn,
            Revision = new RevisionMetadata
            {
                RevisionId = revision.Id,
                PackageVersion = revision.PackageVersion,
                IsApproved = revision.IsApproved,
                PullRequestNo = revision.PullRequestNo,
                PullRequestRepository = pullRequestRepo,
                CreatedBy = revision.CreatedBy,
                CreatedOn = revision.CreatedOn,
                RevisionLink = revisionLink
            }
        };
    }
}
