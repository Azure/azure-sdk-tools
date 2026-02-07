// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Helper class for comparing semantic versions used by the azsdk CLI.
/// Version format: major.minor.patch[-prerelease] (e.g., 1.0.0, 1.0.0-dev.20240101.1)
/// </summary>
public static class VersionHelper
{
    /// <summary>
    /// Compares two version strings and returns true if the remote version is newer than the local version.
    /// </summary>
    public static bool IsNewer(string remote, string local)
    {
        try
        {
            var remoteParts = Parse(remote);
            var localParts = Parse(local);

            // Compare major.minor.patch
            for (int i = 0; i < 3; i++)
            {
                if (remoteParts.numericParts[i] > localParts.numericParts[i])
                {
                    return true;
                }
                if (remoteParts.numericParts[i] < localParts.numericParts[i])
                {
                    return false;
                }
            }

            // Versions are equal in major.minor.patch
            // Compare prerelease: no prerelease > prerelease
            // If both have prerelease, compare lexicographically
            if (string.IsNullOrEmpty(localParts.prerelease) && !string.IsNullOrEmpty(remoteParts.prerelease))
            {
                // Local is stable, remote is prerelease - local is newer
                return false;
            }
            if (!string.IsNullOrEmpty(localParts.prerelease) && string.IsNullOrEmpty(remoteParts.prerelease))
            {
                // Local is prerelease, remote is stable - remote is newer
                return true;
            }
            if (!string.IsNullOrEmpty(localParts.prerelease) && !string.IsNullOrEmpty(remoteParts.prerelease))
            {
                // Both have prerelease, compare lexicographically
                return string.Compare(remoteParts.prerelease, localParts.prerelease, StringComparison.Ordinal) > 0;
            }

            // Both are stable with same version
            return false;
        }
        catch
        {
            // Fallback to string comparison
            return string.Compare(remote, local, StringComparison.Ordinal) > 0;
        }
    }

    /// <summary>
    /// Parses a version string into its numeric parts and optional prerelease suffix.
    /// </summary>
    public static (int[] numericParts, string? prerelease) Parse(string version)
    {
        var parts = new int[3];
        string? prerelease = null;

        var hyphenIndex = version.IndexOf('-');
        var versionPart = hyphenIndex >= 0 ? version[..hyphenIndex] : version;
        if (hyphenIndex >= 0)
        {
            prerelease = version[(hyphenIndex + 1)..];
        }

        var segments = versionPart.Split('.');
        for (int i = 0; i < Math.Min(segments.Length, 3); i++)
        {
            if (int.TryParse(segments[i], out var num))
            {
                parts[i] = num;
            }
        }

        return (parts, prerelease);
    }

    /// <summary>
    /// Returns true if the version string contains a prerelease suffix (contains '-dev').
    /// </summary>
    public static bool IsPrerelease(string version)
    {
        return version.Contains("-dev", StringComparison.OrdinalIgnoreCase);
    }
}
