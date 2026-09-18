using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.CopilotAgents;

public static class ToolHelpers
{
    /// <summary>
    /// Checks canonical containment and rejects every symbolic link or reparse point on the path.
    /// Nonexistent trailing components are allowed for guarded rename destinations.
    /// </summary>
    public static bool IsPathWithinDirectoryWithoutLinks(string baseDirectory, string fullPath)
    {
        try
        {
            if (!Path.IsPathFullyQualified(fullPath))
            {
                return false;
            }

            // Windows normalization drops trailing dots/spaces; reject that spelling before normalization.
            var inputRoot = Path.GetPathRoot(fullPath)!;
            if (fullPath[inputRoot.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is not ("" or "." or "..") && IsInvalidPathSegment(segment)))
            {
                return false;
            }

            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory));
            var candidate = Path.GetFullPath(fullPath);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!candidate.Equals(root, comparison) &&
                !candidate.StartsWith(root + Path.DirectorySeparatorChar, comparison))
            {
                return false;
            }

            var pathRoot = Path.GetPathRoot(candidate)!;
            var current = pathRoot;
            foreach (var segment in candidate[pathRoot.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (segment.Length == 0 || IsInvalidPathSegment(segment))
                {
                    return false;
                }

                current = Path.Combine(current, segment);
                var info = new FileInfo(current);
                if (info.LinkTarget != null ||
                    ((File.Exists(current) || Directory.Exists(current)) &&
                     (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0))
                {
                    return false;
                }
            }

            return Path.GetFullPath(RealPath.GetRealPath(candidate)).Equals(candidate, comparison);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsInvalidPathSegment(string segment) =>
        segment.EndsWith(' ') || segment.EndsWith('.') || segment.Any(char.IsControl) ||
        segment.IndexOfAny([':', '*', '?', '"', '<', '>', '|', '\\']) >= 0;

    /// <summary>
    /// Try to resolve a provided path against a base directory, ensuring the result stays within the base directory.
    /// </summary>
    /// <param name="baseDirectory">The base directory that bounds all operations.</param>
    /// <param name="relativePath">A relative path provided by the user or caller.</param>
    /// <param name="fullPath">Outputs the resolved full path if successful; otherwise it's null.</param>
    /// <returns>True if the path resolves within the base directory; otherwise false.</returns>
    public static bool TryGetSafeFullPath(string baseDirectory, string relativePath, out string fullPath)
    {
        fullPath = default;

        try
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            if (Path.IsPathRooted(relativePath))
            {
                return false;
            }

            var baseFullPath = Path.GetFullPath(baseDirectory);
            var combinedFullPath = Path.GetFullPath(Path.Join(baseFullPath, relativePath));

            var baseWithSep = baseFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!combinedFullPath.StartsWith(baseWithSep, StringComparison.Ordinal) &&
                !string.Equals(combinedFullPath, baseFullPath, StringComparison.Ordinal))
            {
                return false;
            }

            fullPath = combinedFullPath;
            return true;
        }
        catch
        {
            // Catch any path-related exceptions (e.g., invalid chars) and return false per Try* contract.
            return false;
        }
    }
}
