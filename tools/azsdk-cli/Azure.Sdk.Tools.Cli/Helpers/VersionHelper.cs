// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Helper class for comparing semantic versions used by the azsdk CLI.
/// Version format: major.minor.patch[-prerelease] (e.g., 1.0.0, 1.0.0-dev.20240101.1)
/// </summary>
public class VersionHelper : IComparer<string>
{
    public static VersionHelper Default { get; } = new();

    public int Compare(string? x, string? y)
    {
        return x == y ? 0 : IsNewer(x!, y!) ? 1 : -1;
    }

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
                // Both have prerelease, compare using SemVer-compatible comparison
                return ComparePrereleaseVersions(remoteParts.prerelease, localParts.prerelease) > 0;
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

    /// <summary>
    /// Compares two prerelease version strings using SemVer-compatible comparison.
    /// Splits on '.' and compares numeric parts numerically, others lexicographically.
    /// </summary>
    private static int ComparePrereleaseVersions(string a, string b)
    {
        var aParts = a.Split('.');
        var bParts = b.Split('.');

        var maxLen = Math.Max(aParts.Length, bParts.Length);
        for (int i = 0; i < maxLen; i++)
        {
            var aPart = i < aParts.Length ? aParts[i] : "";
            var bPart = i < bParts.Length ? bParts[i] : "";

            // Try to parse both as integers
            var aIsNum = int.TryParse(aPart, out var aNum);
            var bIsNum = int.TryParse(bPart, out var bNum);

            int cmp;
            if (aIsNum && bIsNum)
            {
                // Both numeric - compare numerically
                cmp = aNum.CompareTo(bNum);
            }
            else if (aIsNum != bIsNum)
            {
                // Numeric identifiers have lower precedence than alphanumeric per SemVer
                cmp = aIsNum ? -1 : 1;
            }
            else
            {
                // Both alphanumeric - compare lexicographically
                cmp = string.Compare(aPart, bPart, StringComparison.Ordinal);
            }

            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }
}
