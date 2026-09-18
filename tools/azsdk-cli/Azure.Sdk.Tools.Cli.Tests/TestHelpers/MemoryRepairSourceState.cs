// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using Azure.Sdk.Tools.Cli.Services.Repair;

namespace Azure.Sdk.Tools.Cli.Tests.TestHelpers;

/// <summary>
/// Content-addressed snapshots of real fixture files, without requiring a Git repository.
/// </summary>
internal sealed class MemoryRepairSourceState : IRepairSourceState
{
    private readonly Dictionary<string, Dictionary<string, string>> _trees = [];

    public Task<RepairSourceSnapshot> CaptureAsync(string repositoryRoot, string artifactsPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var files = Directory.GetFiles(repositoryRoot, "*", SearchOption.AllDirectories)
            .ToDictionary(p => Path.GetRelativePath(repositoryRoot, p).Replace('\\', '/'), File.ReadAllText);
        var tree = Hash(files);
        _trees[tree] = files;
        return Task.FromResult(new RepairSourceSnapshot("fixture-head", tree));
    }

    public Task<IReadOnlyList<RepairSourceChange>> GetChangesAsync(
        string repositoryRoot, string beforeTree, string afterTree, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var before = _trees[beforeTree];
        var after = _trees[afterTree];
        IReadOnlyList<RepairSourceChange> changes = before.Keys.Union(after.Keys).Where(p =>
            before.GetValueOrDefault(p) != after.GetValueOrDefault(p)).Select(p =>
            new RepairSourceChange(p, !before.ContainsKey(p) ? "added" : !after.ContainsKey(p) ? "deleted" : "modified",
                before.ContainsKey(p) ? "100644" : "000000", after.ContainsKey(p) ? "100644" : "000000")).ToList();
        return Task.FromResult(changes);
    }

    public async Task<string> GetDiffAsync(string repositoryRoot, string beforeTree, string afterTree, CancellationToken ct)
    {
        var diff = new StringBuilder();
        foreach (var change in await GetChangesAsync(repositoryRoot, beforeTree, afterTree, ct))
        {
            diff.AppendLine($"diff --git a/{change.Path} b/{change.Path}").AppendLine("@@ -1 +1 @@");
            if (_trees[beforeTree].TryGetValue(change.Path, out var oldText))
            {
                foreach (var line in oldText.Split('\n')) { diff.Append('-').AppendLine(line); }
            }
            if (_trees[afterTree].TryGetValue(change.Path, out var newText))
            {
                foreach (var line in newText.Split('\n')) { diff.Append('+').AppendLine(line); }
            }
        }
        return diff.ToString();
    }

    public Task<string> GetSubtreeAsync(string repositoryRoot, string tree, string relativePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var prefix = relativePath.Replace('\\', '/').TrimEnd('/') + "/";
        return Task.FromResult(Hash(_trees[tree].Where(f => f.Key.StartsWith(prefix, StringComparison.Ordinal))));
    }

    private static string Hash(IEnumerable<KeyValuePair<string, string>> files) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\0",
            files.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key}\0{f.Value}")))));
}
