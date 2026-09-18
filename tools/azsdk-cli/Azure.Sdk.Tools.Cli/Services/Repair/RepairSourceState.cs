// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services.Repair;

public class RepairSourceState(IGitCommandHelper git, ILogger<RepairSourceState> logger) : IRepairSourceState
{
    public const string AbsentSubtree = "absent";
    private readonly ConcurrentDictionary<string, string> _artifacts = new(RepairPathBoundary.Comparer);

    public async Task<RepairSourceSnapshot> CaptureAsync(string repositoryRoot, string artifactsPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var root = RepairPathBoundary.AbsolutePath(repositoryRoot);
        var artifacts = RepairPathBoundary.AbsolutePath(artifactsPath);
        RepairPathBoundary.RequireOutside(artifacts, root);
        if (!Directory.Exists(artifacts))
        {
            throw new DirectoryNotFoundException("The repair artifacts directory does not exist.");
        }
        var actualRoot = Scalar(await RunAsync(root, ["rev-parse", "--show-toplevel"], ct));
        if (!RepairPathBoundary.Comparer.Equals(root, Path.TrimEndingDirectorySeparator(Path.GetFullPath(actualRoot))))
        {
            throw new InvalidOperationException("Source snapshots must be captured from the Git repository root.");
        }
        var head = await HeadAsync(root, ct);
        var sparse = await RunAsync(root, ["config", "--bool", "--get", "core.sparseCheckout"], ct, allowMissingConfig: true);
        if (sparse.ExitCode == 0 && Scalar(sparse) != "false")
        {
            throw new InvalidOperationException("Repair snapshots do not support sparse checkouts.");
        }
        var indexEntries = ParseEntries(await RunAsync(root, ["ls-files", "--stage", "-z"], ct), index: true);
        var flags = NulRecords(await RunAsync(root, ["ls-files", "-v", "-z"], ct));
        if (flags.Any(flag => flag.Length < 3 || flag[0] is 'S' or 's'))
        {
            throw new InvalidOperationException("Repair snapshots do not support skip-worktree entries.");
        }
        var headEntries = ParseEntries(await RunAsync(root, ["ls-tree", "-r", "-z", "--full-tree", head], ct), index: false);
        var headModes = headEntries.ToDictionary(entry => entry.Path, entry => entry.Mode, StringComparer.Ordinal);
        var indexPath = Path.Combine(artifacts, $".repair-index-{Guid.NewGuid():N}");
        if (File.Exists(indexPath) || File.Exists(indexPath + ".lock"))
        {
            throw new IOException("The private repair index already exists.");
        }
        try
        {
            await RunAsync(root, ["read-tree", head], ct, indexPath);
            // Preserve newly tracked ignored files and modes that Git maintains in the user's index
            // on filesystems without executable-bit or symlink support, without modifying that index.
            foreach (var entry in indexEntries)
            {
                if (!headModes.TryGetValue(entry.Path, out var mode) || mode != entry.Mode)
                {
                    await RunAsync(root, ["update-index", "--add", "--cacheinfo", $"{entry.Mode},{entry.Oid},{entry.Path}"], ct, indexPath);
                }
            }
            await RunAsync(root, ["add", "-A", "--", "."], ct, indexPath);
            var tree = Scalar(await RunAsync(root, ["write-tree"], ct, indexPath));
            ValidateOid(tree);
            ParseEntries(await RunAsync(root, ["ls-tree", "-r", "-z", "--full-tree", tree], ct, indexPath), index: false);
            var finalHead = await HeadAsync(root, ct);
            if (head != finalHead)
            {
                throw new InvalidOperationException("Git HEAD changed while capturing repair source evidence.");
            }
            _artifacts[root] = artifacts;
            logger.LogDebug("Captured repair source tree {Tree} at HEAD {Head}", tree, head);
            return new RepairSourceSnapshot(head, tree);
        }
        finally
        {
            File.Delete(indexPath);
            File.Delete(indexPath + ".lock");
        }
    }

    public async Task<IReadOnlyList<RepairSourceChange>> GetChangesAsync(string repositoryRoot, string beforeTree, string afterTree, CancellationToken ct)
    {
        ValidateOid(beforeTree);
        ValidateOid(afterTree);
        var records = NulRecords(await RunAsync(RepairPathBoundary.AbsolutePath(repositoryRoot),
            ["diff", "--raw", "-z", "--no-abbrev", "--no-renames", "--no-ext-diff", "--no-textconv", beforeTree, afterTree, "--"], ct));
        if (records.Length % 2 != 0)
        {
            throw new InvalidOperationException("Git returned an incomplete raw source diff.");
        }
        var changes = new List<RepairSourceChange>();
        for (var i = 0; i < records.Length; i += 2)
        {
            var header = records[i].Split(' ');
            if (header.Length != 5 || !header[0].StartsWith(':'))
            {
                throw new InvalidOperationException("Git returned an invalid raw source diff.");
            }
            var oldMode = header[0][1..];
            var newMode = header[1];
            ValidateMode(oldMode, allowMissing: true);
            ValidateMode(newMode, allowMissing: true);
            ValidateOid(header[2]);
            ValidateOid(header[3]);
            var status = header[4] switch
            {
                "A" => "added",
                "M" => "modified",
                "D" => "deleted",
                "T" => "type_changed",
                _ => throw new InvalidOperationException($"Unsupported Git source diff status: '{header[4]}'.")
            };
            ValidateGitPath(records[i + 1]);
            changes.Add(new RepairSourceChange(records[i + 1], status, oldMode, newMode));
        }
        return changes;
    }

    public async Task<string> GetDiffAsync(string repositoryRoot, string beforeTree, string afterTree, CancellationToken ct)
    {
        ValidateOid(beforeTree);
        ValidateOid(afterTree);
        var root = RepairPathBoundary.AbsolutePath(repositoryRoot);
        if (!_artifacts.TryGetValue(root, out var artifacts))
        {
            throw new InvalidOperationException("Capture source state before requesting its persisted diff.");
        }
        RepairPathBoundary.RejectLinks(artifacts);
        RepairPathBoundary.RequireOutside(artifacts, root);
        var outputPath = Path.Combine(artifacts, $".repair-diff-{Guid.NewGuid():N}");
        // ProcessResult is line-oriented. Let Git write the patch directly so CRLF content and
        // binary patches survive unchanged rather than reconstructing them from stdout lines.
        await using (new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
        }
        try
        {
            await RunAsync(root,
                ["diff", "--binary", "--full-index", "--no-ext-diff", "--no-textconv", "--no-renames", "--no-color",
                    "--src-prefix=a/", "--dst-prefix=b/", $"--output={outputPath}", beforeTree, afterTree, "--"], ct);
            return await File.ReadAllTextAsync(outputPath, new UTF8Encoding(false, true), ct);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    public async Task<string> GetSubtreeAsync(string repositoryRoot, string tree, string relativePath, CancellationToken ct)
    {
        ValidateOid(tree);
        var root = RepairPathBoundary.AbsolutePath(repositoryRoot);
        if (relativePath is "" or ".")
        {
            if (Scalar(await RunAsync(root, ["cat-file", "-t", tree], ct)) != "tree")
            {
                throw new InvalidOperationException("The requested source identity is not a Git tree.");
            }
            return tree;
        }
        ValidateGitPath(relativePath);
        var entries = ParseEntries(await RunAsync(root, ["ls-tree", "-z", tree, "--", relativePath], ct), index: false);
        if (entries.Count == 0)
        {
            return AbsentSubtree;
        }
        if (entries.Count != 1 || entries[0].Mode != "040000" || entries[0].Path != relativePath)
        {
            throw new InvalidOperationException("The requested customization source path is not a Git tree.");
        }
        return entries[0].Oid;
    }

    private async Task<string> HeadAsync(string root, CancellationToken ct)
    {
        var head = Scalar(await RunAsync(root, ["rev-parse", "--verify", "HEAD"], ct));
        ValidateOid(head);
        if (Scalar(await RunAsync(root, ["cat-file", "-t", head], ct)) != "commit")
        {
            throw new InvalidOperationException("Repair source HEAD must identify a Git commit.");
        }
        return head;
    }

    private async Task<ProcessResult> RunAsync(string root, string[] args, CancellationToken ct, string? indexPath = null, bool allowMissingConfig = false)
    {
        ct.ThrowIfCancellationRequested();
        var environment = new Dictionary<string, string>
        {
            ["GIT_OPTIONAL_LOCKS"] = "0",
            ["GIT_LITERAL_PATHSPECS"] = "1"
        };
        if (indexPath != null)
        {
            environment["GIT_INDEX_FILE"] = indexPath;
        }
        var result = await git.Run(new GitOptions(args, root, environmentVariables: environment), ct);
        ct.ThrowIfCancellationRequested();
        if (result.ExitCode != 0 && !(allowMissingConfig && result.ExitCode == 1 && result.OutputDetails.Count == 0))
        {
            throw new InvalidOperationException($"Git {args[0]} failed while collecting repair source evidence (exit {result.ExitCode}): {result.Output}");
        }
        return result;
    }

    private static string Scalar(ProcessResult result)
    {
        var lines = result.OutputDetails.Where(line => line.Item1 == StdioLevel.StandardOutput).Select(line => line.Item2).ToArray();
        if (lines.Length != 1 || string.IsNullOrEmpty(lines[0]) || lines[0].IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new InvalidOperationException("Git returned an invalid scalar source identity.");
        }
        return lines[0];
    }

    private static string[] NulRecords(ProcessResult result)
    {
        var lines = result.OutputDetails.Where(line => line.Item1 == StdioLevel.StandardOutput).Select(line => line.Item2).ToArray();
        if (lines.Length == 0)
        {
            return [];
        }
        if (lines.Length != 1 || lines[0].IndexOfAny(['\r', '\n', '\uFFFD']) >= 0)
        {
            throw new InvalidOperationException("Git paths containing line breaks or undecodable characters cannot be represented by the process helper.");
        }
        if (!lines[0].EndsWith('\0'))
        {
            throw new InvalidOperationException("Git returned an incomplete NUL-delimited source listing.");
        }
        return lines[0][..^1].Split('\0');
    }

    private static List<TreeEntry> ParseEntries(ProcessResult result, bool index)
    {
        var entries = new List<TreeEntry>();
        foreach (var record in NulRecords(result))
        {
            var separator = record.IndexOf('\t');
            if (separator < 0)
            {
                throw new InvalidOperationException("Git returned an invalid source tree entry.");
            }
            var fields = record[..separator].Split(' ');
            if (fields.Length != 3)
            {
                throw new InvalidOperationException("Git returned an invalid source tree entry.");
            }
            if (index && fields[2] != "0")
            {
                throw new InvalidOperationException("Repair snapshots do not support unmerged index entries.");
            }
            ValidateMode(fields[0]);
            var oid = fields[index ? 1 : 2];
            ValidateOid(oid);
            var path = record[(separator + 1)..];
            ValidateGitPath(path);
            entries.Add(new TreeEntry(path, fields[0], oid));
        }
        return entries;
    }

    private static void ValidateMode(string mode, bool allowMissing = false)
    {
        if (mode == "160000")
        {
            throw new InvalidOperationException("Repair snapshots do not support submodules or gitlinks.");
        }
        if (mode is not ("100644" or "100755" or "120000" or "040000") && !(allowMissing && mode == "000000"))
        {
            throw new InvalidOperationException($"Unsupported Git source mode: '{mode}'.");
        }
    }

    private static void ValidateOid(string oid)
    {
        if (oid == null || (oid.Length != 40 && oid.Length != 64) || !oid.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException("A full hexadecimal Git object ID is required.", nameof(oid));
        }
    }

    private static void ValidateGitPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\') || path.IndexOfAny(['\r', '\n', '\0', '\uFFFD']) >= 0 ||
            path.Split('/').Any(part => string.IsNullOrEmpty(part) || part is "." or "..") ||
            (OperatingSystem.IsWindows() && path.IndexOfAny(['&', '|', '<', '>', '^', '%', '!', '"']) >= 0))
        {
            throw new InvalidOperationException("Git source paths must be unambiguous repository-relative paths.");
        }
    }

    private record TreeEntry(string Path, string Mode, string Oid);
}
