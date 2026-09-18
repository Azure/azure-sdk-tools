// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Services.Repair;

internal static class RepairPathBoundary
{
    internal static StringComparer Comparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static string AbsolutePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path) || path.Split('/', '\\').Any(part => part is "." or ".."))
        {
            throw new ArgumentException("Repair paths must be absolute and cannot contain '.' or '..' components.", nameof(path));
        }
        if (OperatingSystem.IsWindows() &&
            (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal) ||
             path.Split('/', '\\').Any(part => part.EndsWith('.') || part.EndsWith(' ')) ||
             path.IndexOfAny(['&', '|', '<', '>', '^', '%', '!', '"']) >= 0))
        {
            throw new ArgumentException("Repair paths cannot contain Windows path aliases or command-shell metacharacters.", nameof(path));
        }
        var result = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        RejectLinks(result);
        return result;
    }

    internal static void RejectLinks(string path)
    {
        for (var current = path; current != null; current = Path.GetDirectoryName(current))
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (FileNotFoundException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException($"Repair paths cannot traverse symbolic links or reparse points: '{current}'.");
            }
        }
    }

    internal static void RequireOutside(string artifactsPath, string repositoryRoot)
    {
        if (Contains(repositoryRoot, artifactsPath) || Contains(artifactsPath, repositoryRoot))
        {
            throw new ArgumentException("Repair artifacts must not overlap a source repository.");
        }
    }

    private static bool Contains(string parent, string child)
    {
        var prefix = Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar;
        return Comparer.Equals(parent, child) || child.StartsWith(prefix,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    internal static string RelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var parts = relativePath.Split('/', '\\');
        if (Path.IsPathRooted(relativePath) || parts.Any(part =>
            string.IsNullOrEmpty(part) || part is "." or ".." ||
            part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.Contains(':') ||
            part.EndsWith('.') || part.EndsWith(' ') || IsDeviceName(part)))
        {
            throw new ArgumentException("Artifact filenames must be safe relative paths without traversal.", nameof(relativePath));
        }
        return Path.Combine(parts);
    }

    private static bool IsDeviceName(string part)
    {
        var name = part.Split('.')[0];
        return name.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            (name.Length == 4 && name[3] is >= '0' and <= '9' &&
                (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)));
    }
}
