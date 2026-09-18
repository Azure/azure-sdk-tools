// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Services.Repair;

namespace Azure.Sdk.Tools.Cli.Tests.TestHelpers;

internal sealed class MemoryRepairArtifacts(string directory) : IRepairArtifacts
{
    public Dictionary<string, string> Content { get; } = [];

    public string CreateDirectory(string? requestedPath, string sessionId, string sdkRepositoryRoot, string? localSpecRepositoryRoot)
    {
        Directory.CreateDirectory(directory);
        return directory;
    }

    public Task WriteTextAsync(string artifactsPath, string relativePath, string content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Content[relativePath] = content;
        return Task.CompletedTask;
    }

    public Task WriteJsonAsync<T>(string artifactsPath, string relativePath, T value, CancellationToken ct) =>
        WriteTextAsync(artifactsPath, relativePath, JsonSerializer.Serialize(value), ct);
}
