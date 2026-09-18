// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Services.Repair;

public interface IRepairArtifacts
{
    /// <summary>Creates or claims an empty external directory for one repair invocation.</summary>
    string CreateDirectory(string? requestedPath, string sessionId, string sdkRepositoryRoot, string? localSpecRepositoryRoot);
    /// <summary>Atomically writes a file in a directory previously created by this service instance.</summary>
    Task WriteTextAsync(string artifactsPath, string relativePath, string content, CancellationToken ct);
    Task WriteJsonAsync<T>(string artifactsPath, string relativePath, T value, CancellationToken ct);
}
