// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Result returned by the TypeSpec Customization service.
/// </summary>
public record TypeSpecCustomizationServiceResult
{
    /// <summary>
    /// Whether any customizations were successfully applied.
    /// True if at least one change was applied successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Summary of changes applied to client.tsp, mapped to the request items they address.
    /// e.g., "Renamed FooClient to BarClient for .NET (addresses: 'client name should be BarClient')"
    /// </summary>
    public required string[] ChangesSummary { get; init; }

    /// <summary>
    /// Reason for failure if Success is false (no changes could be applied).
    /// </summary>
    public string? FailureReason { get; init; }
}
