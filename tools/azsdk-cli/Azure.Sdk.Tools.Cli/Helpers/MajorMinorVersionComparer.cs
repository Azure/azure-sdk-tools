// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Compares version strings in "major.minor" format using semantic versioning rules.
/// For example: "1.0" &gt; "0.99", "2.0" &gt; "1.99", "0.99" &gt; "0.1"
/// </summary>
public class MajorMinorVersionComparer : IComparer<string>
{
    /// <summary>
    /// Gets a default instance of the comparer.
    /// </summary>
    public static MajorMinorVersionComparer Default { get; } = new();

    /// <summary>
    /// Compares two version strings in "major.minor" format.
    /// </summary>
    /// <param name="x">First version string</param>
    /// <param name="y">Second version string</param>
    /// <returns>
    /// A negative value if x &lt; y, zero if x == y, or a positive value if x &gt; y.
    /// </returns>
    /// <exception cref="FormatException">Thrown when either version string is not in valid "major.minor" format.</exception>
    public int Compare(string? x, string? y)
    {
        var (xMajor, xMinor) = ParseVersion(x);
        var (yMajor, yMinor) = ParseVersion(y);

        // Compare major version first
        int majorComparison = xMajor.CompareTo(yMajor);
        if (majorComparison != 0)
        {
            return majorComparison;
        }

        // Compare minor version
        return xMinor.CompareTo(yMinor);
    }

    /// <summary>
    /// Parses a version string in "major.minor" format.
    /// </summary>
    /// <param name="version">The version string to parse</param>
    /// <returns>A tuple containing major version and minor version</returns>
    /// <exception cref="FormatException">Thrown when version string is not in valid "major.minor" format.</exception>
    public static (int Major, int Minor) ParseVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new FormatException($"Invalid version format: '{version}'. Expected format is 'major.minor' (e.g., '1.0').");
        }

        var parts = version.Split('.');
        if (parts.Length != 2)
        {
            throw new FormatException($"Invalid version format: '{version}'. Expected format is 'major.minor' (e.g., '1.0').");
        }

        if (!int.TryParse(parts[0], out int major) || !int.TryParse(parts[1], out int minor))
        {
            throw new FormatException($"Invalid version format: '{version}'. Expected format is 'major.minor' (e.g., '1.0').");
        }

        return (major, minor);
    }

    /// <summary>
    /// Returns true if x is greater than y using semantic versioning comparison.
    /// </summary>
    public static bool IsGreaterThan(string? x, string? y) => Default.Compare(x, y) > 0;

    /// <summary>
    /// Returns true if x is less than y using semantic versioning comparison.
    /// </summary>
    public static bool IsLessThan(string? x, string? y) => Default.Compare(x, y) < 0;

    /// <summary>
    /// Returns true if x equals y using semantic versioning comparison.
    /// </summary>
    public static bool AreEqual(string? x, string? y) => Default.Compare(x, y) == 0;
}
