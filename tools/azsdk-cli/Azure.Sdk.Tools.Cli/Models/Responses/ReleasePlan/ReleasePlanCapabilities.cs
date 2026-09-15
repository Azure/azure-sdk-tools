// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

/// <summary>
/// Advisory capabilities for the current caller. Mutations must recheck authorization.
/// </summary>
public sealed class ReleasePlanCapabilities
{
    [JsonPropertyName("can_abandon")]
    public bool CanAbandon { get; init; }

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}