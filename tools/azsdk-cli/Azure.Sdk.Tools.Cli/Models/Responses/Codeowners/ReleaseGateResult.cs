// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

/// <summary>
/// Result of the release-gate check indicating whether a package has sufficient codeowners.
/// </summary>
public class ReleaseGateResult : CommandResponse
{
    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("unique_owner_count")]
    public int UniqueOwnerCount { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Passed ? "PASSED" : "FAILED");
        sb.AppendLine(Message);
        sb.AppendLine($"Unique owners: {UniqueOwnerCount}");
        return sb.ToString();
    }
}
