using System;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Helpers;

public static class VersionNormalizationHelper
{
    /// <summary>
    ///     Normalizes raw package version strings into a canonical <c>VersionIdentifier</c>
    ///     and classifies them as <see cref="VersionKind.Stable" />, <see cref="VersionKind.Preview" />,
    ///     or <see cref="VersionKind.RollingPrerelease" />.
    /// </summary>
    public static (string VersionIdentifier, VersionKind Kind) NormalizeVersion(string packageVersion, string language = null)
    {
        if (string.IsNullOrWhiteSpace(packageVersion))
        {
            throw new ArgumentException("Package version cannot be null or empty.", nameof(packageVersion));
        }

        // Strip leading 'v'/'V' prefix common in some ecosystems.
        string versionIdentifier = packageVersion.TrimStart('v', 'V').Trim();
        var semVer = new AzureEngSemanticVersion(versionIdentifier, language);

        // Unparseable strings (e.g. PEP 440 `.dev`/`.post` qualifiers) fall back to Preview.
        if (!semVer.IsSemVerFormat)
        {
            return (versionIdentifier.ToLowerInvariant(), VersionKind.Preview);

        }

        // No explicit prerelease label — covers stable releases (1.2.0) and sub-1.0.0 (0.5.0).
        if (!semVer.HasPrereleaseLabel)
        {
            string baseId = $"{semVer.Major}.{semVer.Minor}.{semVer.Patch}";
            return semVer.Major >= 1
                ? (baseId, VersionKind.Stable)
                : (baseId, VersionKind.Preview); // sub-1.0.0 treated as preview-stage
        }

        // Daily CI builds embed a YYYYMMDD date stamp as the prerelease number
        // Normalize to a stable channel identifier by stripping the date stamp.
        if (semVer.IsDailyDevBuild)
        {
            string channelId = $"{semVer.Major}.{semVer.Minor}.{semVer.Patch}-{semVer.PrereleaseLabel.ToLowerInvariant()}";
            return (channelId, VersionKind.RollingPrerelease);
        }

        // Milestone prerelease (e.g. 1.2.0-beta.1, 1.2.0b2, 0.5.0-beta.1).
        string label = semVer.PrereleaseLabel.ToLowerInvariant();
        string identifier = $"{semVer.Major}.{semVer.Minor}.{semVer.Patch}" +
                            $"{semVer.PrereleaseLabelSeparator}{label}" +
                            $"{semVer.PrereleaseNumberSeparator}{semVer.PrereleaseNumber}";
        return (identifier, VersionKind.Preview);
    }
}
