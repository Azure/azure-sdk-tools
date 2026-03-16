// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.TestHelpers;

/// <summary>
/// Utility to create and reliably clean up a temporary directory for tests.
/// Implements both IDisposable and IAsyncDisposable so it can be used with
/// either synchronous or asynchronous test flows. Deletion is retried to
/// tolerate transient file locks on some platforms.
/// </summary>
public sealed class TempDirectory : IDisposable, IAsyncDisposable
{
    public string DirectoryPath { get; }
    private bool _disposed;

    private TempDirectory(string directoryPath)
    {
        // RealPath.GetRealPath resolves symlinks but returns NormalizedPath (forward slashes).
        // Wrap in Path.GetFullPath to convert back to platform-native separators so that
        // test paths match assertions against Path.GetFullPath / Directory.GetFiles etc.
        DirectoryPath = Path.GetFullPath((string)RealPath.GetRealPath(directoryPath));
        Directory.CreateDirectory(directoryPath);
    }

    /// <summary>
    /// Creates a new temporary directory with an optional prefix.
    /// </summary>
    public static TempDirectory Create(string? prefix = null)
    {
        var name = (prefix ?? "azsdk-temp") + "_" + Guid.NewGuid().ToString("N");
        var fullPath = Path.Combine(Path.GetTempPath(), name);
        return new TempDirectory(fullPath);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Task.Run(() => TryDeleteWithRetries(DirectoryPath));
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private static void TryDeleteWithRetries(string path, int attempts = 3, int initialDelayMs = 1)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        for (int i = 0; i < attempts; i++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                return;
            }
            catch (IOException) when (i < attempts - 1)
            {
                Thread.Sleep(initialDelayMs * (i + 1));
            }
            catch (UnauthorizedAccessException) when (i < attempts - 1)
            {
                Thread.Sleep(initialDelayMs * (i + 1));
            }
        }
    }
}
