// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Response for the upgrade command.
/// </summary>
public class UpgradeResponse : CommandResponse
{
    [JsonPropertyName("old_version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OldVersion { get; set; }

    [JsonPropertyName("new_version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NewVersion { get; set; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    /// <summary>
    /// Whether a restart is required to use the new version.
    /// Particularly relevant for MCP server mode.
    /// </summary>
    [JsonPropertyName("restart_required")]
    public bool RestartRequired { get; set; }

    /// <summary>
    /// The download URL used for the upgrade.
    /// </summary>
    [JsonPropertyName("download_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DownloadUrl { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Upgrade from {OldVersion ?? "unknown version"} to {NewVersion ?? "unknown version"}");
        if (!string.IsNullOrEmpty(DownloadUrl))        {
            sb.AppendLine($"Download URL: {DownloadUrl}");
        }
        sb.AppendLine(Message);

        return sb.ToString();
    }
}
