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
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (RepoRoot, RelativePath, FullPath).</returns>
    public static async Task<(string RepoRoot, string RelativePath, string FullPath)> ParseAsync(IGitHelper gitHelper, string realPackagePath, CancellationToken ct = default)
    {
        var full = RealPath.GetRealPath(realPackagePath);
        var repoRoot = await gitHelper.DiscoverRepoRootAsync(full, ct);
        var sdkRoot = Path.Combine(repoRoot, "sdk");
        var relativePath = Path.GetRelativePath(sdkRoot, full).TrimStart(Path.DirectorySeparatorChar);
        return (repoRoot, relativePath, full);
    }
}
