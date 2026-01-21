// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Services.APIView;

/// <summary>
/// Shared helper for parsing APIView URLs to extract review and revision IDs.
/// </summary>
public static partial class ApiViewUrlParser
{
    /// <summary>
    /// Extracts review ID and revision ID from an APIView URL.
    /// </summary>
    /// <param name="url">The full APIView URL (e.g., https://apiview.dev/review/{reviewId}?activeApiRevisionId={revisionId})</param>
    /// <returns>A tuple containing (revisionId, reviewId)</returns>
    /// <exception cref="ArgumentException">Thrown when the URL is invalid or missing required components</exception>
    public static (string revisionId, string reviewId) ExtractIds(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("APIView URL cannot be null or empty", nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new ArgumentException(
                "Input needs to be a valid APIView URL (e.g., https://apiview.dev/review/{reviewId}?activeApiRevisionId={revisionId})",
                nameof(url));
        }

        // Pattern: /review/{reviewId} in path and activeApiRevisionId={revisionId} in query string
        var match = ApiViewUrlRegex().Match(url);

        if (!match.Success)
        {
            throw new ArgumentException(
                "APIView URL must contain both '/review/{reviewId}' path segment AND 'activeApiRevisionId' query parameter",
                nameof(url));
        }

        string reviewId = match.Groups[1].Value;
        string revisionId = match.Groups[2].Value;

        return (revisionId, reviewId);
    }

    [GeneratedRegex(@"/review/([^/?]+).*[?&]activeApiRevisionId=([^&#]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ApiViewUrlRegex();
}
