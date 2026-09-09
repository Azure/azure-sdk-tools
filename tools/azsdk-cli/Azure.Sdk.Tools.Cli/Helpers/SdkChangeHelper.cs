// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;

namespace Azure.Sdk.Tools.Cli.Helpers;

public static class SdkChangeHelper
{
    public static async Task<SdkChange> ReadFromFileAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var change = await JsonSerializer.DeserializeAsync<SdkChange>(stream, cancellationToken: ct);
        if (change == null || string.IsNullOrWhiteSpace(change.SdkChangeMD))
        {
            throw new JsonException($"SDK change file '{path}' must contain nonempty changes (Markdown) and a Boolean hasBreakingChange.");
        }
        return change;
    }
}
