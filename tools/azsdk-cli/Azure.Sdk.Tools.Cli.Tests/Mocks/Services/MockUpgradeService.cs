// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tests.Mocks.Services;

public class MockUpgradeService : IUpgradeService
{
    public string CurrentVersion { get; set; } = "1.0.0";
    public bool IsPrerelease { get; set; } = false;
    public string? LatestVersion { get; set; } = null;

    public string GetCurrentVersion() => CurrentVersion;

    public string GetReleaseNotesUrl(string version) => "https://release_notes_test_url";

    public bool IsCurrentVersionPrerelease() => IsPrerelease;

    public Task<string?> CheckLatestVersion(bool includePrerelease, bool failSilently, bool ignoreCache, CancellationToken ct)
    {
        return Task.FromResult(LatestVersion);
    }

    public Task<bool> TryShowUpgradeNotification(CancellationToken ct)
    {
        return Task.FromResult(false);
    }

    public Task<UpgradeResponse> Upgrade(string? targetVersion, bool includePrerelease, CancellationToken ct)
    {
        return Task.FromResult(new UpgradeResponse
        {
            Succeeded = true,
            OldVersion = CurrentVersion,
            NewVersion = targetVersion ?? LatestVersion ?? CurrentVersion,
            Message = "Mock upgrade succeeded"
        });
    }

    public Task<UpgradeResponse> CompleteUpgrade(string targetPath, CancellationToken ct)
    {
        return Task.FromResult(new UpgradeResponse
        {
            Succeeded = true,
            OldVersion = CurrentVersion,
            NewVersion = CurrentVersion,
            Message = "Mock upgrade completed"
        });
    }
}
