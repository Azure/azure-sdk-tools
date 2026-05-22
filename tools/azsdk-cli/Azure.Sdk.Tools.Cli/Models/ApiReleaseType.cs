// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models;

public enum ApiReleaseType
{
    Unknown,
    PrivatePreview,
    PublicPreview,
    GA
}

public static class ApiReleaseTypeExtensions
{
    private const string AdoPrivatePreview = "APEX Private Preview";
    private const string AdoPublicPreview = "APEX Public Preview";
    private const string AdoGA = "GA";

    /// <summary>
    /// Converts a user-supplied string to an ApiReleaseType enum value (case-insensitive).
    /// </summary>
    public static bool TryParseFromUserInput(string? input, out ApiReleaseType result)
    {
        result = ApiReleaseType.Unknown;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (trimmed.Equals("Private Preview", StringComparison.OrdinalIgnoreCase))
        {
            result = ApiReleaseType.PrivatePreview;
        }
        if (trimmed.Equals("Public Preview", StringComparison.OrdinalIgnoreCase))
        {
            result = ApiReleaseType.PublicPreview;
        }
        if (trimmed.Equals("GA", StringComparison.OrdinalIgnoreCase))
        {
            result = ApiReleaseType.GA;
        }
        return result != ApiReleaseType.Unknown;
    }

    /// <summary>
    /// Converts an ADO work item field value to an ApiReleaseType enum value.
    /// </summary>
    public static ApiReleaseType FromAdoFieldValue(string? adoValue)
    {
        if (string.IsNullOrWhiteSpace(adoValue))
        {
            return ApiReleaseType.Unknown;
        }

        if (adoValue.Equals(AdoPrivatePreview, StringComparison.OrdinalIgnoreCase))
        {
            return ApiReleaseType.PrivatePreview;
        }

        if (adoValue.Equals(AdoPublicPreview, StringComparison.OrdinalIgnoreCase))
        {
            return ApiReleaseType.PublicPreview;
        }

        if (adoValue.Equals(AdoGA, StringComparison.OrdinalIgnoreCase))
        {
            return ApiReleaseType.GA;
        }

        return ApiReleaseType.Unknown;
    }

    /// <summary>
    /// Returns the ADO work item field value for this release type.
    /// </summary>
    public static string ToAdoFieldValue(this ApiReleaseType releaseType) => releaseType switch
    {
        ApiReleaseType.PrivatePreview => AdoPrivatePreview,
        ApiReleaseType.PublicPreview => AdoPublicPreview,
        ApiReleaseType.GA => AdoGA,
        _ => string.Empty
    };

    /// <summary>
    /// Returns a display-friendly label (used in titles, user messages).
    /// </summary>
    public static string ToDisplayLabel(this ApiReleaseType releaseType) => releaseType switch
    {
        ApiReleaseType.PrivatePreview => "Private Preview",
        ApiReleaseType.PublicPreview => "Public Preview",
        ApiReleaseType.GA => "GA",
        _ => string.Empty
    };

    /// <summary>
    /// Returns the default SDK release type for the given API release type.
    /// </summary>
    public static string GetDefaultSdkReleaseType(this ApiReleaseType releaseType) =>
        releaseType == ApiReleaseType.GA ? "stable" : "beta";

    /// <summary>
    /// Validates whether a spec PR URL is compatible with this release type.
    /// Returns null if valid, or an error message if invalid.
    /// </summary>
    public static string? ValidateSpecPullRequest(this ApiReleaseType releaseType, string specPullRequestUrl)
    {
        if (string.IsNullOrEmpty(specPullRequestUrl) || releaseType == ApiReleaseType.Unknown)
        {
            return null;
        }

        var isPrivateSpec = specPullRequestUrl.Contains("azure-rest-api-specs-pr", StringComparison.OrdinalIgnoreCase);

        if (releaseType == ApiReleaseType.PrivatePreview && !isPrivateSpec)
        {
            return "A spec pull request in azure-rest-api-specs (public) cannot be linked to a Private Preview release plan. " +
                   "Use a spec PR from azure-rest-api-specs-pr for Private Preview, or choose Public Preview or GA release type for a public spec PR.";
        }

        if ((releaseType == ApiReleaseType.PublicPreview || releaseType == ApiReleaseType.GA) && isPrivateSpec)
        {
            return "A spec pull request in azure-rest-api-specs-pr (private) cannot be linked to a Public Preview or GA release plan. " +
                   "Use a spec PR from azure-rest-api-specs for Public Preview or GA, or choose Private Preview release type for a private spec PR.";
        }

        return null;
    }
}
