// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Utility for obtaining a normalized absolute path with symbolic link / reparse point resolution.
/// </summary>
/// <remarks>
/// <para>
/// This helper walks a path segment by segment, resolving at most <c>maxDepth</c> symbolic link hops.
/// It purposely resolves *one* hop at a time so that chained links (A -> B -> C ...) each consume depth,
/// allowing detection of pathological cycles or very deep chains. When a link is encountered its target
/// is spliced together with any remaining yet-to-be-processed segments and traversal restarts on the
/// new absolute path. This approach avoids recursively expanding the entire chain in one call which could
/// mask link depth.
/// </para>
/// <para>
/// If a segment does not exist (file nor directory) the remainder of the original input is appended and
/// the resulting fully qualified path is returned without further existence checks. This means the method
/// can be used for paths that are partially materialized on disk (e.g. preparing output paths) while still
/// canonicalizing the portion that does exist and resolving links encountered early in the chain.
/// </para>
/// <para>
/// The implementation uses <see cref="FileSystemInfo.LinkTarget"/> where supported. On platforms where
/// link resolution API throws <see cref="PlatformNotSupportedException"/>, a manual fallback combines the
/// (possibly relative) <c>LinkTarget</c> with the base directory. All returned paths are passed through
/// <see cref="Path.GetFullPath(string)"/> for normalization.
/// </para>
/// <para>
/// Safety guards:
/// <list type="bullet">
///   <item><description><c>segmentVisits</c> is capped (2048) to avoid infinite enumeration loops.</description></item>
///   <item><description><c>linkResolves</c> is capped by <c>maxDepth</c> to prevent excessive / cyclic link chains.</description></item>
/// </list>
/// </para>
/// <para>
/// Example:
/// <code>
/// var real = RealPath.GetRealPath("./some/symlink/../target");
/// Console.WriteLine(real); // => /abs/path/to/target (after resolving symlink and normalizing)
/// </code>
/// </para>
/// </remarks>
public static class RealPath
{
    /// <summary>
    /// Returns an absolute, normalized path for <paramref name="path"/> with up to <paramref name="maxDepth"/>
    /// symbolic link hops resolved. The returned path uses forward slashes for cross-platform consistency.
    /// </summary>
    /// <param name="path">The input path. May be relative or absolute. Must not be null, empty, or whitespace.</param>
    /// <param name="maxDepth">Maximum number of link hops to resolve. Acts as a guard against cyclic or excessive link chains. Default is 64.</param>
    /// <returns>The fully qualified path with resolved link hops and normalized to forward slashes.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null, empty, or whitespace.</exception>
    /// <exception cref="IOException">
    /// Thrown when too many segments are processed (potential loop), when more than <paramref name="maxDepth"/> link hops
    /// are encountered, or when a link target cannot be resolved.
    /// </exception>
    /// <remarks>
    /// Link resolution is performed one hop at a time; chains longer than <paramref name="maxDepth"/> trigger an <see cref="IOException"/>.
    /// Non-existent tail segments are appended without validation so callers can use this for paths being created.
    /// </remarks>
    public static NormalizedPath GetRealPath(string path, int maxDepth = 64)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is null or empty.", nameof(path));
        }

        var sep = Path.DirectorySeparatorChar;

        // We iterate over segments (root + parts). When a link is resolved,
        // we rebuild the segment list from the new absolute path and continue.
        string[] segments = EnumerateSegments(path).ToArray();
        if (segments.Length == 0)
        {
            return Path.GetFullPath(path); // defensive; shouldn't happen
        }

        string acc = segments[0]; // root (drive/UNC or "/" on Unix)
        int segmentVisits = 0;    // traversal safety cap
        int linkResolves = 0;     // count of link hops (cycle/chain guard)

        for (int i = 1; i < segments.Length; i++)
        {
            if (++segmentVisits > 2048)
            {
                throw new IOException("Too many path segments processed (possible loop).");
            }

            acc = Path.Combine(acc, segments[i]);

            // If this segment doesn't exist yet, append the rest and return normalized.
            if (!File.Exists(acc) && !Directory.Exists(acc))
            {
                var tail = string.Join(sep, segments, i + 1, segments.Length - (i + 1));
                return Path.GetFullPath(tail.Length == 0 ? acc : Path.Combine(acc, tail));
            }

            // Choose file vs directory info based on what actually exists.
            FileSystemInfo fsi = Directory.Exists(acc)
                ? new DirectoryInfo(acc)
                : new FileInfo(acc);

            // Not a link? keep going.
            if (string.IsNullOrEmpty(fsi.LinkTarget))
            {
                continue;
            }

            // Count link hops and resolve exactly ONE hop so chains consume depth.
            if (++linkResolves > maxDepth)
            {
                throw new IOException("Too many nested links encountered (possible cycle).");
            }

            string hopPath;
            try
            {
                // One-hop resolution to respect maxDepth on chained links
                var hop = fsi.ResolveLinkTarget(returnFinalTarget: false);
                if (hop is null)
                {
                    throw new IOException($"Unable to resolve link target for '{acc}'.");
                }

                hopPath = hop.FullName;
            }
            catch (PlatformNotSupportedException)
            {
                // Fallback: manually resolve using LinkTarget (which may be relative)
                string linkTarget = fsi.LinkTarget!;
                if (!Path.IsPathRooted(linkTarget))
                {
                    var baseDir =
                        (fsi as FileInfo)?.Directory?.FullName ??
                        (fsi as DirectoryInfo)?.Parent?.FullName ??
                        Path.GetDirectoryName(acc) ??
                        segments[0]; // root

                    hopPath = Path.GetFullPath(Path.Combine(baseDir, linkTarget));
                }
                else
                {
                    hopPath = Path.GetFullPath(linkTarget);
                }
            }

            // Combine the hop target with any remaining segments and restart traversal.
            var remaining = string.Join(sep, segments, i + 1, segments.Length - (i + 1));
            var newPath = remaining.Length == 0 ? hopPath : Path.GetFullPath(Path.Combine(hopPath, remaining));

            segments = EnumerateSegments(newPath).ToArray();
            acc = segments[0];
            i = 0; // loop will ++ to 1 and continue with the first non-root segment
        }

        // Ensure normalized absolute path
        return Path.GetFullPath(acc);
    }

    /// <summary>
    /// Splits a path into a sequence of segments where the first element is the root
    /// (drive/UNC on Windows, "/" on Unix), followed by each directory/file name.
    /// </summary>
    /// <summary>
    /// Enumerates path segments for a given path, yielding the root (drive / UNC / "/") as the first element
    /// followed by each directory or file component in order.
    /// </summary>
    /// <param name="path">The path to split. May be relative; it is first normalized to a full path.</param>
    /// <returns>An enumerable whose first element is the root and subsequent elements are individual components.</returns>
    /// <remarks>
    /// Implements its own splitting logic instead of <see cref="string.Split(char[])"/> to avoid allocating empty segments
    /// and to treat alternate directory separators uniformly.
    /// </remarks>
    private static IEnumerable<string> EnumerateSegments(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? string.Empty;

        if (root.Length == 0)
        {
            // Relative path with no root: simulate by using current directory root
            full = Path.GetFullPath(full);
            root = Path.GetPathRoot(full) ?? string.Empty;
        }

        yield return root;

        var remainder = full[root.Length..];

        int start = 0;
        for (int idx = 0; idx <= remainder.Length; idx++)
        {
            bool atEnd = idx == remainder.Length;
            bool isSep = !atEnd && (remainder[idx] == Path.DirectorySeparatorChar || remainder[idx] == Path.AltDirectorySeparatorChar);

            if (atEnd || isSep)
            {
                if (idx > start)
                {
                    yield return remainder.Substring(start, idx - start);
                }
                start = idx + 1;
            }
        }
    }
}
