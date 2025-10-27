// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Provides common parsing logic for SDK package paths across languages (except Go which has specialized rules).
/// Validates that a given real package path resides under the repo's 'sdk' directory and returns
/// the repository root, relative path under 'sdk', and the full resolved path.
/// </summary>
internal static class PackagePathParser
{
    /// <summary>
    /// Parse a package path.
    /// Expected general structure: &lt;repoRoot&gt;/sdk/&lt;service&gt;/&lt;package&gt; (language-specific depth checks may vary).
    /// </summary>
    /// <param name="gitHelper">Git helper used to discover repository root.</param>
    /// <param name="realPackagePath">Real (fully resolved) package path.</param>
    /// <returns>Tuple of (RepoRoot, RelativePath, FullPath).</returns>
    public static (string RepoRoot, string RelativePath, string FullPath) Parse(IGitHelper gitHelper, string realPackagePath)
    {
        var full = new DirectoryInfo(realPackagePath).FullName;
        var repoRoot = gitHelper.DiscoverRepoRoot(full);
        var sdkRoot = Path.Combine(repoRoot, "sdk");
        var relativePath = Path.GetRelativePath(sdkRoot, full).TrimStart(Path.DirectorySeparatorChar);
        return (repoRoot, relativePath, full);
    }
}
