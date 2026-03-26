// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Represents the cached upgrade check state persisted to a global config file
/// </summary>
public class CliUpgradeCache
{
    /// <summary>
    /// The local version at the time of last check.
    /// </summary>
    [JsonPropertyName("local_version")]
    public string? LocalVersion { get; set; }

    /// <summary>
    /// The latest remote version found on GitHub Releases.
    /// </summary>
    [JsonPropertyName("remote_version")]
    public string? RemoteVersion { get; set; }

    /// <summary>
    /// When the remote version was last fetched from GitHub.
    /// </summary>
    [JsonPropertyName("last_remote_refresh_utc")]
    public DateTimeOffset? LastRemoteRefreshUtc { get; set; }

    /// <summary>
    /// When the user was last notified about an available update.
    /// </summary>
    [JsonPropertyName("last_notify_utc")]
    public DateTimeOffset? LastNotifyUtc { get; set; }

    /// <summary>
    /// Whether the cached remote version is a prerelease.
    /// </summary>
    [JsonPropertyName("is_prerelease")]
    public bool IsPrerelease { get; set; }

    /// <summary>
    /// The source of the remote version (e.g. a URL)
    /// </summary>
    [JsonPropertyName("remote_source")]
    public string? RemoteSource { get; set; }
}
