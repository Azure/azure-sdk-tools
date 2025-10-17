// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class RealPath
{
    public static string GetRealPath(string path, int maxDepth = 64)
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

        // NOTE: Avoid ReadOnlySpan<char> across yield boundaries (CS4007). Using string instead.
        var remainder = full.Substring(root.Length);
        var ds = Path.DirectorySeparatorChar;
        var ads = Path.AltDirectorySeparatorChar;

        int start = 0;
        for (int idx = 0; idx <= remainder.Length; idx++)
        {
            bool atEnd = idx == remainder.Length;
            bool isSep = !atEnd && (remainder[idx] == ds || remainder[idx] == ads);

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
