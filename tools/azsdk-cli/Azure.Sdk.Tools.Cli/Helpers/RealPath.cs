// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers;

public static class RealPath
{
    public static string GetRealPath(string path, int maxDepth = 64)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is null or empty.", nameof(path));
        }

        var full = Path.GetFullPath(path);
        var sep = Path.DirectorySeparatorChar;

        string root = Path.GetPathRoot(full)!;
        string remainder = full.Substring(root.Length);
        string[] parts = remainder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

        string acc = root;
        int segmentVisits = 0;   // tracks traversal work
        int linkResolves = 0;    // tracks link resolutions (cycle guard)

        for (int i = 0; i < parts.Length; i++)
        {
            if (++segmentVisits > 2048) // generous safety cap
            {
                throw new IOException("Too many path segments processed (possible loop).");
            }

            acc = Path.Combine(acc, parts[i]);

            if (!File.Exists(acc) && !Directory.Exists(acc))
            {
                var tail = string.Join(sep, parts, i + 1, parts.Length - (i + 1));
                return Path.GetFullPath(tail.Length == 0 ? acc : Path.Combine(acc, tail));
            }

            FileSystemInfo fsi = Directory.Exists(acc)
                ? new DirectoryInfo(acc)
                : new FileInfo(acc);

            // Not a link? keep going.
            if (fsi.LinkTarget is null || fsi.LinkTarget.Length == 0)
            {
                continue;
            }

            if (++linkResolves > maxDepth)
            {
                throw new IOException("Too many nested links encountered (possible cycle).");
            }

            // Prefer ResolveLinkTarget(true) when available
            string targetPath;
            try
            {
                var resolved = fsi.ResolveLinkTarget(returnFinalTarget: true);
                targetPath = resolved?.FullName ?? throw new IOException($"Unable to resolve link target for '{acc}'.");
            }
            catch (PlatformNotSupportedException)
            {
                // Fallback to LinkTarget + manual resolution
                string linkTarget = fsi.LinkTarget!;
                if (!Path.IsPathRooted(linkTarget))
                {
                    var baseDir =
                        (fsi as FileInfo)?.Directory?.FullName ??
                        (fsi as DirectoryInfo)?.Parent?.FullName ??
                        Path.GetDirectoryName(acc) ??
                        root;

                    targetPath = Path.GetFullPath(Path.Combine(baseDir, linkTarget));
                }
                else
                {
                    targetPath = Path.GetFullPath(linkTarget);
                }
            }

            var remaining = string.Join(sep, parts, i + 1, parts.Length - (i + 1));
            var newPath = remaining.Length == 0 ? targetPath : Path.GetFullPath(Path.Combine(targetPath, remaining));

            // Restart traversal on the new path
            full = newPath;
            root = Path.GetPathRoot(full)!;
            remainder = full.Substring(root.Length);
            parts = remainder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            acc = root;
            i = -1; // loop will ++ to 0
        }

        // Ensure normalized absolute path
        return Path.GetFullPath(acc.Length == 0 ? root : acc);
    }
}
