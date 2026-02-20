using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Upgrade;

public interface IUpgradeService
{
    /// <summary>
    /// Checks for the latest version, using cache if fresh or fetching from GitHub.
    /// Updates the cache with the result (even on failure to avoid retrying too frequently).
    /// </summary>
    Task<string?> CheckLatestVersion(bool includePrerelease, bool failSilently, bool ignoreCacheTtl, CancellationToken ct);

    /// <summary>
    /// Attempts to show an upgrade notification if one is available and the notify throttle has expired.
    /// Returns true if a notification was shown.
    /// </summary>
    Task<bool> TryShowUpgradeNotification(CancellationToken ct);

    /// <summary>
    /// Performs the upgrade to the specified version (or latest if null).
    /// </summary>
    Task<UpgradeResponse> Upgrade(string? targetVersion, bool includePrerelease, CancellationToken ct);

    /// <summary>
    /// Completes the cross-platform two-step upgrade by waiting for the target executable path to be replaceable and then copying self over it.
    /// </summary>
    Task<UpgradeResponse> CompleteUpgrade(string targetPath, CancellationToken ct);

    /// <summary>
    /// Gets the current installed version.
    /// </summary>
    string GetCurrentVersion();

    /// <summary>
    /// Gets the URL for the release notes of the specified version.
    /// </summary>
    string GetReleaseNotesUrl(string version);

    /// <summary>
    /// Returns true if the current version is a prerelease version (contains '-dev').
    /// </summary>
    bool IsCurrentVersionPrerelease();
}