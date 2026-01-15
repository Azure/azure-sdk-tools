using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
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

    public async Task<RevisionResolveResult> ResolveByPackageAsync(
        string packageName,
        string language,
        string version = null)
    {
        if (string.IsNullOrEmpty(packageName))
        {
            throw new ArgumentException("Package name is required.", nameof(packageName));
        }

        if (string.IsNullOrEmpty(language))
        {
            throw new ArgumentException("Language is required.", nameof(language));
        }

        //TODO: Add package name check after this is done: https://github.com/Azure/azure-sdk-tools/issues/13557

        var review = await _reviewManager.GetReviewAsync(language, packageName, null);
        if (review == null)
        {
            _logger.LogWarning("Review not found for package: {PackageName}, language: {Language}", packageName,
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

        return new RevisionResolveResult { ReviewId = revision.ReviewId, RevisionId = revision.Id };
    }

    public async Task<RevisionResolveResult> ResolveByLinkAsync(string link)
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

            return new RevisionResolveResult { ReviewId = revision.ReviewId, RevisionId = revision.Id };
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

        return new RevisionResolveResult { ReviewId = latestRevision.ReviewId, RevisionId = latestRevision.Id };
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
