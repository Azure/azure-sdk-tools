using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Managers;

public class ResolvePackage : IResolvePackage
{
    private readonly IAPIRevisionsManager _apiRevisionsManager;
    private readonly ILogger<ResolvePackage> _logger;
    private readonly IReviewManager _reviewManager;
    private readonly ICopilotAuthenticationService _copilotAuthService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _copilotEndpoint;

    public ResolvePackage(
        IReviewManager reviewManager,
        IAPIRevisionsManager apiRevisionsManager,
        ICopilotAuthenticationService copilotAuthService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ResolvePackage> logger)
    {
        _reviewManager = reviewManager;
        _apiRevisionsManager = apiRevisionsManager;
        _copilotAuthService = copilotAuthService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _copilotEndpoint = configuration["CopilotServiceEndpoint"];
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
            var client = _httpClientFactory.CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_copilotEndpoint}/api-review/resolve-package");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _copilotAuthService.GetAccessTokenAsync());
            request.Content = JsonContent.Create(new { packageQuery, language });

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ResolvePackageResponse>();
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
