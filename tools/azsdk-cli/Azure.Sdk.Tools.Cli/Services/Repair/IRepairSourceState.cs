// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Services.Repair;

public interface IRepairSourceState
{
    Task<RepairSourceSnapshot> CaptureAsync(string repositoryRoot, string artifactsPath, CancellationToken ct);
    Task<IReadOnlyList<RepairSourceChange>> GetChangesAsync(string repositoryRoot, string beforeTree, string afterTree, CancellationToken ct);
    /// <summary>
    /// Returns an exact UTF-8 Git patch, using the external artifacts directory from a prior capture
    /// for this repository. Unrepresentable output fails rather than returning a corrupted patch.
    /// </summary>
    Task<string> GetDiffAsync(string repositoryRoot, string beforeTree, string afterTree, CancellationToken ct);
    /// <summary>Returns a directory's tree OID, or <see cref="RepairSourceState.AbsentSubtree"/> when it is absent.</summary>
    Task<string> GetSubtreeAsync(string repositoryRoot, string tree, string relativePath, CancellationToken ct);
}
