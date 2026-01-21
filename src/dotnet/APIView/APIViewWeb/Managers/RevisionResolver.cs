using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Managers;

public class RevisionResolver : IRevisionResolver
{
    private readonly IAPIRevisionsManager _apiRevisionsManager;
    private readonly ILogger<RevisionResolver> _logger;
    private readonly IReviewManager _reviewManager;

    public RevisionResolver(
        IReviewManager reviewManager,
        IAPIRevisionsManager apiRevisionsManager,
        ILogger<RevisionResolver> logger)
    {
        _reviewManager = reviewManager;
        _apiRevisionsManager = apiRevisionsManager;
        _logger = logger;
    }

    public async Task<ResolvePackageResponse> ResolvePackageQuery(
        string packageQuery,
        string language,
        string version = null)
    {
        if (string.IsNullOrEmpty(packageQuery))
        {
            throw new ArgumentException("Package name is required.", nameof(packageQuery));
        }

        if (string.IsNullOrEmpty(language))
        {
            throw new ArgumentException("Language is required.", nameof(language));
        }

        ReviewListItemModel review = await _reviewManager.GetReviewAsync(language, packageQuery, null);
        if (review == null)
        {
            //TODO: this is the section in where the call is done directly to copilot as I wasn't able to find an exact match with the packageQuery that I already had
            // will return copilot response
            _logger.LogWarning("Review not found for package: {PackageName}, language: {Language}", packageQuery,
                language);
            return null;
        }

        APIRevisionListItemModel revision;
        if (!string.IsNullOrEmpty(version))
        {
            var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(review.Id, version);
            revision = revisions.FirstOrDefault();

            if (revision == null)
            {
                return null;
            }
        }
        else
        {
            revision = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(review.Id,
                apiRevisionType: APIRevisionType.Automatic);
        }

        if (revision == null)
        {
            _logger.LogWarning("No revisions found for review: {ReviewId}", review.Id);
            return null;
        }

        return new ResolvePackageResponse
        {
            PackageName = review.PackageName,
            Language = review.Language,
            ReviewId = review.Id,
            Version = revision.PackageVersion,
            RevisionId = revision.Id,
            RevisionLabel = revision.Label
        };
    }

    public async Task<ResolvePackageResponse> ResolvePackageLink(string link)
    {
        if (string.IsNullOrEmpty(link))
        {
            throw new ArgumentException("Link is required.", nameof(link));
        }

        var (reviewId, revisionId) = ParseLink(link);

        if (string.IsNullOrEmpty(reviewId))
        {
            _logger.LogWarning("Could not parse review ID from link: {Link}", link);
            return null;
        }

        if (!string.IsNullOrEmpty(revisionId))
        {
            var revision = await _apiRevisionsManager.GetAPIRevisionAsync(revisionId);
            if (revision == null)
            {
                _logger.LogWarning("Revision not found for ID: {RevisionId}", revisionId);
                return null;
            }

            return new ResolvePackageResponse
            {
                PackageName = revision.PackageName,
                Language = revision.Language,
                ReviewId = revision.ReviewId,
                Version = revision.PackageVersion,
                RevisionId = revision.Id,
                RevisionLabel = revision.Label
            };
        }

        ReviewListItemModel review = (await _reviewManager.GetReviewsAsync([reviewId])).FirstOrDefault();
        if (review == null)
        {
            _logger.LogWarning("Review not found for ID: {ReviewId}", reviewId);
            return null;
        }

        APIRevisionListItemModel latestRevision = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(review.Id,
                apiRevisionType: APIRevisionType.Automatic);
        if (latestRevision == null)
        {
            _logger.LogWarning("No revisions found for review: {ReviewId}", reviewId);
            return null;
        }

        return new ResolvePackageResponse
        {
            PackageName = latestRevision.PackageName,
            Language = latestRevision.Language,
            ReviewId = latestRevision.ReviewId,
            Version = latestRevision.PackageVersion,
            RevisionId = latestRevision.Id,
            RevisionLabel = latestRevision.Label
        };
    }

    /// <summary>
    ///     Parses an APIView URL to extract the review ID and optional revision ID.
    ///     Supports formats:
    ///     - https://spa.apiview.dev/review/{reviewId}
    ///     - https://spa.apiview.dev/review/{reviewId}?activeApiRevisionId={revisionId}
    ///     - https://apiview.dev/Assemblies/Review/{reviewId}
    /// </summary>
    private (string reviewId, string revisionId) ParseLink(string link)
    {
        try
        {
            var uri = new Uri(link);
            string reviewId = null;

            string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2 && segments[0].Equals("review", StringComparison.OrdinalIgnoreCase))
            {
                reviewId = segments[1];
            }
            else if (segments.Length >= 3 &&
                     segments[0].Equals("Assemblies", StringComparison.OrdinalIgnoreCase) &&
                     segments[1].Equals("Review", StringComparison.OrdinalIgnoreCase))
            {
                reviewId = segments[2];
            }

            NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);
            string revisionId = query["activeApiRevisionId"];

            return (reviewId, revisionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse link: {Link}", link);
            return (null, null);
        }
    }
}
