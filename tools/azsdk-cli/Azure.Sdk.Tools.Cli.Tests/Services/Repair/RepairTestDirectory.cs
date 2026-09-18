// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Tests.Services.Repair;

internal sealed class RepairTestDirectory : IDisposable
{
    public string Root { get; } = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"repair-tests-{Guid.NewGuid():N}");

    public RepairTestDirectory()
    {
        Directory.CreateDirectory(Root);
    }

    public string Create(string name)
    {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        DeleteDirectory(Root);
    }

    private static void DeleteDirectory(string path)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    Directory.Delete(entry);
                }
                else
                {
                    File.Delete(entry);
                }
            }
            else if ((attributes & FileAttributes.Directory) != 0)
            {
                DeleteDirectory(entry);
            }
            else
            {
                File.SetAttributes(entry, FileAttributes.Normal);
                File.Delete(entry);
            }
        }
        Directory.Delete(path);
    }
}
