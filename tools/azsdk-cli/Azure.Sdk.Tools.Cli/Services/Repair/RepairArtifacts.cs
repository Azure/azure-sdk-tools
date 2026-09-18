// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Services.Repair;

public class RepairArtifacts : IRepairArtifacts
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ConcurrentDictionary<string, string[]> _directories = new(RepairPathBoundary.Comparer);

    public string CreateDirectory(string? requestedPath, string sessionId, string sdkRepositoryRoot, string? localSpecRepositoryRoot)
    {
        var safeSessionId = RepairPathBoundary.RelativePath(sessionId);
        if (safeSessionId.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new ArgumentException("The repair session ID must be a single path component.", nameof(sessionId));
        }
        var path = RepairPathBoundary.AbsolutePath(requestedPath ??
            Path.Combine(Path.GetTempPath(), "azsdk-repair", safeSessionId));
        var roots = localSpecRepositoryRoot == null
            ? new[] { RepairPathBoundary.AbsolutePath(sdkRepositoryRoot) }
            : new[] { RepairPathBoundary.AbsolutePath(sdkRepositoryRoot), RepairPathBoundary.AbsolutePath(localSpecRepositoryRoot) };
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException($"The repair source repository does not exist: '{root}'.");
            }
            RepairPathBoundary.RequireOutside(path, root);
        }
        if (File.Exists(path) || (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any()))
        {
            throw new IOException("The repair artifacts directory must be new or empty.");
        }
        Directory.CreateDirectory(path);
        RepairPathBoundary.RejectLinks(path);
        if (!_directories.TryAdd(path, roots))
        {
            throw new IOException("The repair artifacts directory already belongs to a repair invocation.");
        }
        return path;
    }

    public Task WriteJsonAsync<T>(string artifactsPath, string relativePath, T value, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return WriteTextAsync(artifactsPath, relativePath, JsonSerializer.Serialize(value, JsonOptions), ct);
    }

    public async Task WriteTextAsync(string artifactsPath, string relativePath, string content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var root = RepairPathBoundary.AbsolutePath(artifactsPath);
        if (!_directories.TryGetValue(root, out var sourceRoots))
        {
            throw new InvalidOperationException("The artifacts directory must be created by this repair artifacts service.");
        }
        foreach (var sourceRoot in sourceRoots)
        {
            RepairPathBoundary.RejectLinks(sourceRoot);
            RepairPathBoundary.RequireOutside(root, sourceRoot);
        }
        var target = Path.Combine(root, RepairPathBoundary.RelativePath(relativePath));
        RepairPathBoundary.RejectLinks(target);
        var parent = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(parent);
        RepairPathBoundary.RejectLinks(parent);
        var pending = Path.Combine(parent, $".repair-write-{Guid.NewGuid():N}");
        var owned = false;
        try
        {
            await using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous))
            {
                owned = true;
                var bytes = Encoding.UTF8.GetBytes(content);
                await stream.WriteAsync(bytes, ct);
                await stream.FlushAsync(ct);
            }
            ct.ThrowIfCancellationRequested();
            RepairPathBoundary.RejectLinks(target);
            File.Move(pending, target, overwrite: true);
        }
        finally
        {
            if (owned)
            {
                File.Delete(pending);
            }
        }
    }
}
