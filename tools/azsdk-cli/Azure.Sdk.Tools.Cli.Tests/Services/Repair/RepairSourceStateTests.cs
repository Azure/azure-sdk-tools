// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services.Repair;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Repair;

[TestFixture]
public class RepairSourceStateTests
{
    private RepairTestDirectory _directory = null!;
    private string _repo = null!;
    private string _artifacts = null!;
    private RecordingGit _git = null!;
    private RepairSourceState _state = null!;

    [SetUp]
    public async Task SetUp()
    {
        _directory = new RepairTestDirectory();
        _repo = _directory.Create("repo");
        _artifacts = _directory.Create("artifacts");
        var configuration = Path.Combine(_directory.Root, "empty-config");
        await File.WriteAllTextAsync(configuration, "");
        _git = new RecordingGit(configuration);
        _state = new RepairSourceState(_git, new TestLogger<RepairSourceState>());
        await Git("init");
        await Git("config", "user.email", "repair@test.invalid");
        await Git("config", "user.name", "Repair tests");
        await Git("config", "core.autocrlf", "false");
        await Git("config", "core.filemode", "false");
        await Git("config", "core.longpaths", "true");
        await File.WriteAllTextAsync(Path.Combine(_repo, "source.cs"), "original\n");
        await File.WriteAllTextAsync(Path.Combine(_repo, "delete.cs"), "deleted later\n");
        await File.WriteAllTextAsync(Path.Combine(_repo, "rename.cs"), "renamed later\n");
        await File.WriteAllTextAsync(Path.Combine(_repo, ".gitignore"), "*.ignored\nbin/\n");
        Directory.CreateDirectory(Path.Combine(_repo, "custom"));
        await File.WriteAllTextAsync(Path.Combine(_repo, "custom", "custom.cs"), "custom code\n");
        await Git("add", "-A");
        await Git("commit", "-m", "Initial test source");
    }

    [TearDown]
    public void TearDown() => _directory.Dispose();

    private Task<RepairSourceSnapshot> Capture(CancellationToken ct = default) => _state.CaptureAsync(_repo, _artifacts, ct);

    private async Task<ProcessResult> Git(params string[] args)
    {
        var result = await _git.Run(new GitOptions(args, _repo), CancellationToken.None);
        Assert.That(result.ExitCode, Is.Zero, $"git {string.Join(' ', args)}: {result.Output}");
        return result;
    }

    private string IndexPath => Path.Combine(_repo, ".git", "index");

    [Test]
    public async Task Capture_IncludesWorkingSourceButNotIgnoredOutputsAndPreservesStaging()
    {
        var initial = await Capture();
        await File.WriteAllTextAsync(Path.Combine(_repo, "source.cs"), "staged\n");
        await File.WriteAllTextAsync(Path.Combine(_repo, "staged.cs"), "staged addition\n");
        await Git("add", "source.cs", "staged.cs");
        await File.WriteAllTextAsync(Path.Combine(_repo, "source.cs"), "unstaged after staging\n");
        await File.WriteAllTextAsync(Path.Combine(_repo, "staged.cs"), "unstaged addition content\n");
        await File.WriteAllTextAsync(Path.Combine(_repo, "new Ω source.cs"), "new source\n");
        await File.WriteAllTextAsync(Path.Combine(_repo, "build.ignored"), "ignored build output");
        Directory.CreateDirectory(Path.Combine(_repo, "bin"));
        await File.WriteAllTextAsync(Path.Combine(_repo, "bin", "output.cs"), "ignored output");
        File.Delete(Path.Combine(_repo, "delete.cs"));
        File.Move(Path.Combine(_repo, "rename.cs"), Path.Combine(_repo, "renamed.cs"));
        var index = await File.ReadAllBytesAsync(IndexPath);
        var stagedDiff = (await Git("diff", "--cached")).Stdout;

        var captured = await Capture();
        var changes = await _state.GetChangesAsync(_repo, initial.Tree, captured.Tree, CancellationToken.None);
        var diff = await _state.GetDiffAsync(_repo, initial.Tree, captured.Tree, CancellationToken.None);

        Assert.That(captured.Head, Is.EqualTo(initial.Head));
        Assert.That(captured.Tree, Is.Not.EqualTo(initial.Tree));
        Assert.That(changes.Select(change => (change.Path, change.Status)), Is.EquivalentTo(new[]
        {
            ("source.cs", "modified"), ("staged.cs", "added"), ("new Ω source.cs", "added"),
            ("delete.cs", "deleted"), ("rename.cs", "deleted"), ("renamed.cs", "added")
        }));
        Assert.That(changes.Single(change => change.Path == "source.cs").OldMode, Is.EqualTo("100644"));
        Assert.That(changes.Single(change => change.Path == "staged.cs").OldMode, Is.EqualTo("000000"));
        Assert.That(changes.Single(change => change.Path == "delete.cs").NewMode, Is.EqualTo("000000"));
        Assert.That(diff, Does.Contain("+unstaged after staging"));
        Assert.That(diff, Does.Contain("+unstaged addition content"));
        Assert.That(diff, Does.Contain("deleted file mode"));
        Assert.That(diff, Does.Contain("new file mode"));
        Assert.That(await File.ReadAllBytesAsync(IndexPath), Is.EqualTo(index));
        Assert.That((await Git("diff", "--cached")).Stdout, Is.EqualTo(stagedDiff));
        Assert.That(Directory.GetFileSystemEntries(_artifacts), Is.Empty);
    }

    [Test]
    public async Task Capture_IncludesTrackedIgnoredFilesAndStagedIgnoredAdditions()
    {
        await File.WriteAllTextAsync(Path.Combine(_repo, "tracked.ignored"), "original\n");
        await Git("add", "-f", "tracked.ignored");
        await Git("commit", "-m", "Track ignored source");
        var initial = await Capture();
        await File.WriteAllTextAsync(Path.Combine(_repo, "tracked.ignored"), "modified\n");
        await File.WriteAllTextAsync(Path.Combine(_repo, "staged.ignored"), "staged\n");
        await Git("add", "-f", "staged.ignored");
        await File.WriteAllTextAsync(Path.Combine(_repo, "staged.ignored"), "unstaged new ignored source\n");
        var index = await File.ReadAllBytesAsync(IndexPath);

        var captured = await Capture();
        var changes = await _state.GetChangesAsync(_repo, initial.Tree, captured.Tree, CancellationToken.None);
        Assert.That(changes.Select(change => (change.Path, change.Status)), Is.EquivalentTo(new[]
        {
            ("tracked.ignored", "modified"), ("staged.ignored", "added")
        }));
        Assert.That((await Git("show", $"{captured.Tree}:staged.ignored")).Stdout, Is.EqualTo("unstaged new ignored source"));
        Assert.That(await File.ReadAllBytesAsync(IndexPath), Is.EqualTo(index));
    }

    [Test]
    public async Task Capture_SupportsLinkedWorktreesWithoutChangingEitherUserIndex()
    {
        var originalIndexPath = IndexPath;
        var originalIndex = await File.ReadAllBytesAsync(originalIndexPath);
        var linked = Path.Combine(_directory.Root, "linked");
        await Git("worktree", "add", "-b", "linked", linked);
        _repo = linked;
        var linkedIndexPath = (await Git("rev-parse", "--git-path", "index")).Stdout;
        var linkedIndex = await File.ReadAllBytesAsync(linkedIndexPath);
        var before = await Capture();
        await File.WriteAllTextAsync(Path.Combine(_repo, "source.cs"), "linked change\n");
        var after = await Capture();

        Assert.That(await _state.GetChangesAsync(_repo, before.Tree, after.Tree, CancellationToken.None),
            Is.EqualTo(new[] { new RepairSourceChange("source.cs", "modified", "100644", "100644") }));
        Assert.That(await File.ReadAllBytesAsync(originalIndexPath), Is.EqualTo(originalIndex));
        Assert.That(await File.ReadAllBytesAsync(linkedIndexPath), Is.EqualTo(linkedIndex));
    }

    [Test]
    public async Task Capture_UnchangedSourceHasStableIdentityDespiteIgnoredChanges()
    {
        var before = await Capture();
        await File.WriteAllTextAsync(Path.Combine(_repo, "new.ignored"), "ignored");
        var after = await Capture();
        Assert.That(after, Is.EqualTo(before));
        Assert.That(await _state.GetChangesAsync(_repo, before.Tree, after.Tree, CancellationToken.None), Is.Empty);
        Assert.That(await _state.GetDiffAsync(_repo, before.Tree, after.Tree, CancellationToken.None), Is.Empty);
    }

    [Test]
    public async Task Capture_PreservesExecutableModeMaintainedInUserIndex()
    {
        var initial = await Capture();
        await Git("update-index", "--chmod=+x", "source.cs");
        var index = await File.ReadAllBytesAsync(IndexPath);
        var captured = await Capture();
        var changes = await _state.GetChangesAsync(_repo, initial.Tree, captured.Tree, CancellationToken.None);

        Assert.That(changes, Is.EqualTo(new[] { new RepairSourceChange("source.cs", "modified", "100644", "100755") }));
        Assert.That(await File.ReadAllBytesAsync(IndexPath), Is.EqualTo(index));
    }

    [Test]
    public async Task Capture_PreservesFilesystemExecutableModeWhereSupported()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Windows does not expose POSIX executable file modes.");
            return;
        }
        await Git("config", "core.filemode", "true");
        var before = await Capture();
        var path = Path.Combine(_repo, "source.cs");
        File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute);
        var after = await Capture();
        var change = (await _state.GetChangesAsync(_repo, before.Tree, after.Tree, CancellationToken.None)).Single();
        Assert.That(change.OldMode, Is.EqualTo("100644"));
        Assert.That(change.NewMode, Is.EqualTo("100755"));
    }

    [Test]
    public async Task Capture_ReportsSymlinkTypeChangeWhereSupported()
    {
        var before = await Capture();
        var path = Path.Combine(_repo, "source.cs");
        File.Delete(path);
        try
        {
            File.CreateSymbolicLink(path, "custom/custom.cs");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            Assert.Ignore($"Symbolic links are unavailable: {ex.Message}");
        }
        await Git("config", "core.symlinks", "true");
        var after = await Capture();
        var changes = await _state.GetChangesAsync(_repo, before.Tree, after.Tree, CancellationToken.None);
        Assert.That(changes, Is.EqualTo(new[] { new RepairSourceChange("source.cs", "type_changed", "100644", "120000") }));
    }

    [Test]
    public async Task GetDiff_PreservesCrLfAndBinaryDataAndProducesApplicablePatch()
    {
        await File.WriteAllTextAsync(Path.Combine(_repo, "source.cs"), "old\r\n");
        await File.WriteAllBytesAsync(Path.Combine(_repo, "data.bin"), [0, 1, 2, 3, 4, 5]);
        await Git("add", "-A");
        await Git("commit", "-m", "Binary and CRLF source");
        var before = await Capture();
        await File.WriteAllTextAsync(Path.Combine(_repo, "source.cs"), "new\r\n");
        await File.WriteAllBytesAsync(Path.Combine(_repo, "data.bin"), [0, 7, 8, 9, 10, 11]);
        var after = await Capture();

        var diff = await _state.GetDiffAsync(_repo, before.Tree, after.Tree, CancellationToken.None);
        Assert.That(diff, Does.Contain("-old\r\n+new\r\n"));
        Assert.That(diff, Does.Contain("GIT binary patch"));
        Assert.That(Directory.GetFileSystemEntries(_artifacts), Is.Empty);
        var patch = Path.Combine(_artifacts, "actual.diff");
        await File.WriteAllTextAsync(patch, diff);
        await Git("apply", "--check", "--reverse", patch);
    }

    [Test]
    public async Task GetDiff_DisablesExternalDiffAndTextConversion()
    {
        await File.WriteAllTextAsync(Path.Combine(_repo, ".gitattributes"), "*.cs diff=repair-test\n");
        await Git("config", "diff.repair-test.command", "must-not-execute");
        await Git("config", "diff.repair-test.textconv", "must-not-execute");
        await Git("add", ".gitattributes");
        await Git("commit", "-m", "Diff driver configuration");
        var before = await Capture();
        await File.WriteAllTextAsync(Path.Combine(_repo, "source.cs"), "changed source\n");
        var after = await Capture();
        var diff = await _state.GetDiffAsync(_repo, before.Tree, after.Tree, CancellationToken.None);
        Assert.That(diff, Does.Contain("+changed source"));
        Assert.That(await _state.GetChangesAsync(_repo, before.Tree, after.Tree, CancellationToken.None), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetDiff_FailureCleansScratchWithoutDeletingExistingEvidence()
    {
        var snapshot = await Capture();
        var evidence = Path.Combine(_artifacts, "previous.diff");
        await File.WriteAllTextAsync(evidence, "previous evidence");
        _git.Intercept = (_, _) =>
        {
            var result = new ProcessResult { ExitCode = 128 };
            result.AppendStderr("diff failed");
            return Task.FromResult<ProcessResult?>(result);
        };
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _state.GetDiffAsync(_repo, snapshot.Tree, snapshot.Tree, CancellationToken.None));
        Assert.That(Directory.GetFileSystemEntries(_artifacts), Is.EqualTo(new[] { evidence }));
        Assert.That(await File.ReadAllTextAsync(evidence), Is.EqualTo("previous evidence"));
    }

    [Test]
    public async Task GetSubtree_ReturnsDirectoryOidOrExplicitAbsenceAndRejectsBlob()
    {
        var snapshot = await Capture();
        var subtree = await _state.GetSubtreeAsync(_repo, snapshot.Tree, "custom", CancellationToken.None);
        Assert.That(subtree, Is.EqualTo((await Git("rev-parse", $"{snapshot.Tree}:custom")).Stdout));
        Assert.That(await _state.GetSubtreeAsync(_repo, snapshot.Tree, "absent", CancellationToken.None), Is.EqualTo(RepairSourceState.AbsentSubtree));
        Assert.That(await _state.GetSubtreeAsync(_repo, snapshot.Tree, ".", CancellationToken.None), Is.EqualTo(snapshot.Tree));
        Assert.ThrowsAsync<InvalidOperationException>(() => _state.GetSubtreeAsync(_repo, snapshot.Tree, "source.cs", CancellationToken.None));
        Assert.ThrowsAsync<InvalidOperationException>(() => _state.GetSubtreeAsync(_repo, new string('1', 40), "custom", CancellationToken.None));
    }

    [Test]
    public async Task GetSubtree_UsesLiteralPathspecRatherThanWildcards()
    {
        var path = Path.Combine(_repo, "custom[1]");
        Directory.CreateDirectory(path);
        await File.WriteAllTextAsync(Path.Combine(path, "file.cs"), "literal");
        var snapshot = await Capture();
        Assert.That(await _state.GetSubtreeAsync(_repo, snapshot.Tree, "custom[1]", CancellationToken.None),
            Is.Not.EqualTo(RepairSourceState.AbsentSubtree));
        Assert.That(await _state.GetSubtreeAsync(_repo, snapshot.Tree, "custom*", CancellationToken.None),
            Is.EqualTo(RepairSourceState.AbsentSubtree));
    }

    [TestCase("--help")]
    [TestCase("HEAD")]
    [TestCase("1234")]
    [TestCase("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaag")]
    public void TreeOperations_RejectNonOidArgumentsBeforeInvokingGit(string invalid)
    {
        var valid = new string('a', 40);
        _git.Calls.Clear();
        Assert.ThrowsAsync<ArgumentException>(() => _state.GetChangesAsync(_repo, invalid, valid, CancellationToken.None));
        Assert.ThrowsAsync<ArgumentException>(() => _state.GetChangesAsync(_repo, valid, invalid, CancellationToken.None));
        Assert.ThrowsAsync<ArgumentException>(() => _state.GetDiffAsync(_repo, invalid, valid, CancellationToken.None));
        Assert.ThrowsAsync<ArgumentException>(() => _state.GetSubtreeAsync(_repo, invalid, "custom", CancellationToken.None));
        Assert.That(_git.Calls, Is.Empty);
    }

    [TestCase("../custom")]
    [TestCase("/custom")]
    [TestCase("custom/../source.cs")]
    [TestCase("custom\\file")]
    public void GetSubtree_RejectsUnsafeRelativePath(string path)
    {
        _git.Calls.Clear();
        Assert.ThrowsAsync<InvalidOperationException>(() => _state.GetSubtreeAsync(_repo, new string('a', 40), path, CancellationToken.None));
        Assert.That(_git.Calls, Is.Empty);
    }

    [Test]
    public void GetSubtree_RejectsWindowsShellMetacharactersBeforeInvokingGit()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("The existing process helper uses cmd.exe only on Windows.");
        }
        _git.Calls.Clear();
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _state.GetSubtreeAsync(_repo, new string('a', 40), "custom&other", CancellationToken.None));
        Assert.That(_git.Calls, Is.Empty);
    }

    [Test]
    public async Task Capture_RejectsSparseCheckoutAndPreservesIndex()
    {
        await Git("config", "core.sparseCheckout", "true");
        var index = await File.ReadAllBytesAsync(IndexPath);
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => Capture());
        Assert.That(ex!.Message, Does.Contain("sparse"));
        Assert.That(await File.ReadAllBytesAsync(IndexPath), Is.EqualTo(index));
    }

    [Test]
    public async Task Capture_RejectsSkipWorktreeEvenWithoutSparseConfiguration()
    {
        await Git("update-index", "--skip-worktree", "source.cs");
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => Capture());
        Assert.That(ex!.Message, Does.Contain("skip-worktree"));
    }

    [Test]
    public async Task Capture_RejectsUnmergedIndex()
    {
        var branch = (await Git("branch", "--show-current")).Stdout;
        await Git("checkout", "-b", "conflict");
        await File.WriteAllTextAsync(Path.Combine(_repo, "source.cs"), "branch\n");
        await Git("add", "source.cs");
        await Git("commit", "-m", "Conflicting branch");
        await Git("checkout", branch);
        await File.WriteAllTextAsync(Path.Combine(_repo, "source.cs"), "main\n");
        await Git("add", "source.cs");
        await Git("commit", "-m", "Conflicting main");
        var merge = await _git.Run(new GitOptions(["merge", "conflict"], _repo), CancellationToken.None);
        Assert.That(merge.ExitCode, Is.Not.Zero);
        var index = await File.ReadAllBytesAsync(IndexPath);
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => Capture());
        Assert.That(ex!.Message, Does.Contain("unmerged"));
        Assert.That(await File.ReadAllBytesAsync(IndexPath), Is.EqualTo(index));
    }

    [Test]
    public async Task Capture_RejectsGitlinkInIndexAndHead()
    {
        var commit = (await Git("rev-parse", "HEAD")).Stdout;
        await Git("update-index", "--add", "--cacheinfo", $"160000,{commit},nested");
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => Capture());
        Assert.That(ex!.Message, Does.Contain("gitlinks"));
        await Git("commit", "-m", "Gitlink");
        await Git("update-index", "--force-remove", "nested");
        ex = Assert.ThrowsAsync<InvalidOperationException>(() => Capture());
        Assert.That(ex!.Message, Does.Contain("gitlinks"));
    }

    [Test]
    public async Task Capture_RejectsNewUntrackedNestedRepository()
    {
        var nested = Path.Combine(_repo, "nested");
        Directory.CreateDirectory(nested);
        foreach (var args in new[]
        {
            new[] { "init" },
            ["config", "user.email", "nested@test.invalid"],
            ["config", "user.name", "Nested tests"],
            ["config", "core.longpaths", "true"],
            ["commit", "--allow-empty", "-m", "Nested commit"]
        })
        {
            var result = await _git.Run(new GitOptions(args, nested), CancellationToken.None);
            Assert.That(result.ExitCode, Is.Zero, result.Output);
        }
        var index = await File.ReadAllBytesAsync(IndexPath);
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => Capture());
        Assert.That(ex!.Message, Does.Contain("gitlinks"));
        Assert.That(await File.ReadAllBytesAsync(IndexPath), Is.EqualTo(index));
        Assert.That(Directory.GetFileSystemEntries(_artifacts), Is.Empty);
    }

    [Test]
    public async Task Capture_SupportsSha256Repositories()
    {
        _repo = _directory.Create("sha256-repo");
        var initialized = await _git.Run(new GitOptions(["init", "--object-format=sha256"], _repo), CancellationToken.None);
        if (initialized.ExitCode != 0)
        {
            Assert.Ignore("Installed Git does not support SHA-256 repositories.");
        }
        await Git("config", "user.email", "repair@test.invalid");
        await Git("config", "user.name", "Repair tests");
        await Git("config", "core.longpaths", "true");
        await Git("commit", "--allow-empty", "-m", "Initial SHA-256 commit");
        var before = await Capture();
        await File.WriteAllTextAsync(Path.Combine(_repo, "source.cs"), "sha256\n");
        var after = await Capture();

        Assert.That(after.Head.Length, Is.EqualTo(64));
        Assert.That(after.Tree.Length, Is.EqualTo(64));
        Assert.That(await _state.GetChangesAsync(_repo, before.Tree, after.Tree, CancellationToken.None),
            Is.EqualTo(new[] { new RepairSourceChange("source.cs", "added", "000000", "100644") }));
        Assert.That(await _state.GetSubtreeAsync(_repo, after.Tree, "missing", CancellationToken.None), Is.EqualTo(RepairSourceState.AbsentSubtree));
    }

    [Test]
    public void Capture_RejectsRealNewlineFilenameWhereSupported()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Windows does not support newline filenames.");
        }
        File.WriteAllText(Path.Combine(_repo, "line\nbreak.cs"), "source");
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => Capture());
        Assert.That(ex!.Message, Does.Contain("line breaks"));
        Assert.That(Directory.GetFileSystemEntries(_artifacts), Is.Empty);
    }

    [Test]
    public void Capture_RejectsArtifactOverlapAndNonRootWorkingDirectory()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _state.CaptureAsync(_repo, _repo, CancellationToken.None));
        Assert.ThrowsAsync<ArgumentException>(() => _state.CaptureAsync(_repo, _directory.Root, CancellationToken.None));
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _state.CaptureAsync(Path.Combine(_repo, "custom"), _artifacts, CancellationToken.None));
    }

    [Test]
    public void Capture_RejectsArtifactsSymlink()
    {
        var link = Path.Combine(_directory.Root, "link");
        try
        {
            Directory.CreateSymbolicLink(link, _repo);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            Assert.Ignore($"Symbolic links are unavailable: {ex.Message}");
        }
        Assert.ThrowsAsync<InvalidOperationException>(() => _state.CaptureAsync(_repo, link, CancellationToken.None));
    }

    [Test]
    public async Task Capture_RejectsHeadChangeWhilePreservingUserIndex()
    {
        var index = await File.ReadAllBytesAsync(IndexPath);
        var reads = 0;
        var changedHead = new string('a', 40);
        _git.Intercept = (options, _) =>
        {
            if (options.SubCommand == "rev-parse" && options.Args.Contains("HEAD") && ++reads == 2)
            {
                var result = new ProcessResult();
                result.AppendStdout(changedHead);
                return Task.FromResult<ProcessResult?>(result);
            }
            if (options.SubCommand == "cat-file" && options.Args.Contains(changedHead))
            {
                var result = new ProcessResult();
                result.AppendStdout("commit");
                return Task.FromResult<ProcessResult?>(result);
            }
            return Task.FromResult<ProcessResult?>(null);
        };
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => Capture());
        Assert.That(ex!.Message, Does.Contain("HEAD changed"));
        Assert.That(await File.ReadAllBytesAsync(IndexPath), Is.EqualTo(index));
        Assert.That(Directory.GetFileSystemEntries(_artifacts), Is.Empty);
    }

    [TestCase("read-tree")]
    [TestCase("add")]
    [TestCase("write-tree")]
    public async Task Capture_CommandFailureCleansOnlyOwnedIndexAndSurfacesError(string command)
    {
        var unrelated = Path.Combine(_artifacts, "unrelated.index");
        await File.WriteAllTextAsync(unrelated, "unrelated evidence");
        var index = await File.ReadAllBytesAsync(IndexPath);
        _git.Intercept = (options, _) =>
        {
            if (options.SubCommand == command)
            {
                var owned = options.EnvironmentVariables!["GIT_INDEX_FILE"];
                File.WriteAllText(owned + ".lock", "partial lock");
                var result = new ProcessResult { ExitCode = 128 };
                result.AppendStderr("synthetic Git failure");
                return Task.FromResult<ProcessResult?>(result);
            }
            return Task.FromResult<ProcessResult?>(null);
        };

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => Capture());
        Assert.That(ex!.Message, Does.Contain(command).And.Contain("synthetic Git failure"));
        Assert.That(await File.ReadAllBytesAsync(IndexPath), Is.EqualTo(index));
        Assert.That(Directory.GetFileSystemEntries(_artifacts), Is.EqualTo(new[] { unrelated }));
    }

    [Test]
    public async Task Capture_CancellationRethrowsAndCleansOwnedIndex()
    {
        using var cts = new CancellationTokenSource();
        var index = await File.ReadAllBytesAsync(IndexPath);
        _git.Intercept = (options, ct) =>
        {
            if (options.SubCommand == "add")
            {
                File.WriteAllText(options.EnvironmentVariables!["GIT_INDEX_FILE"] + ".lock", "partial");
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
            }
            return Task.FromResult<ProcessResult?>(null);
        };
        Assert.ThrowsAsync<OperationCanceledException>(() => Capture(cts.Token));
        Assert.That(await File.ReadAllBytesAsync(IndexPath), Is.EqualTo(index));
        Assert.That(Directory.GetFileSystemEntries(_artifacts), Is.Empty);
    }

    [Test]
    public void Capture_PreCancellationDoesNotInvokeGit()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _git.Calls.Clear();
        Assert.ThrowsAsync<OperationCanceledException>(() => Capture(cts.Token));
        Assert.That(_git.Calls, Is.Empty);
    }

    [Test]
    public async Task Capture_AlwaysUsesExternalPrivateIndexForMutatingCommands()
    {
        _git.Calls.Clear();
        await Capture();
        var mutations = _git.Calls.Where(options => options.SubCommand is "read-tree" or "update-index" or "add" or "write-tree").ToList();
        Assert.That(mutations.Count, Is.GreaterThanOrEqualTo(3));
        foreach (var call in mutations)
        {
            Assert.That(call.WorkingDirectory, Is.EqualTo(_repo));
            Assert.That(call.EnvironmentVariables!["GIT_INDEX_FILE"], Does.StartWith(_artifacts + Path.DirectorySeparatorChar));
            Assert.That(call.EnvironmentVariables["GIT_OPTIONAL_LOCKS"], Is.EqualTo("0"));
        }
        Assert.That(mutations.Select(call => call.EnvironmentVariables!["GIT_INDEX_FILE"]).Distinct().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task GetChanges_RejectsLineBrokenPathRatherThanNormalizingIt()
    {
        var snapshot = await Capture();
        _git.Intercept = (options, _) =>
        {
            if (options.SubCommand == "diff")
            {
                var result = new ProcessResult();
                result.AppendStdout($":100644 100644 {snapshot.Tree} {snapshot.Tree} M\0broken");
                result.AppendStdout("filename.cs\0");
                return Task.FromResult<ProcessResult?>(result);
            }
            return Task.FromResult<ProcessResult?>(null);
        };
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
            _state.GetChangesAsync(_repo, snapshot.Tree, snapshot.Tree, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("line breaks"));
    }

    [Test]
    public async Task GetChanges_IgnoresStderrWhenParsingNulDelimitedOutput()
    {
        var snapshot = await Capture();
        _git.Intercept = (options, _) =>
        {
            if (options.SubCommand == "diff")
            {
                var result = new ProcessResult();
                result.AppendStderr("harmless warning");
                result.AppendStdout($":100644 100755 {snapshot.Tree} {snapshot.Tree} M\0spaces and\ttabs.cs\0");
                return Task.FromResult<ProcessResult?>(result);
            }
            return Task.FromResult<ProcessResult?>(null);
        };
        Assert.That(await _state.GetChangesAsync(_repo, snapshot.Tree, snapshot.Tree, CancellationToken.None),
            Is.EqualTo(new[] { new RepairSourceChange("spaces and\ttabs.cs", "modified", "100644", "100755") }));
    }

    [TestCase("not a raw diff\0file.cs\0")]
    [TestCase(":100644 100644 a b M\0file.cs\0")]
    [TestCase(":100644 100644 a b M\0")]
    [TestCase("unterminated")]
    public async Task GetChanges_RejectsMalformedOutput(string output)
    {
        var snapshot = await Capture();
        _git.Intercept = (options, _) =>
        {
            var result = new ProcessResult();
            result.AppendStdout(output);
            return Task.FromResult<ProcessResult?>(result);
        };
        Assert.That(async () => await _state.GetChangesAsync(_repo, snapshot.Tree, snapshot.Tree, CancellationToken.None),
            Throws.InstanceOf<Exception>());
    }

    [Test]
    public async Task GetSubtree_DoesNotTranslateGitFailureIntoAbsent()
    {
        var snapshot = await Capture();
        _git.Intercept = (_, _) =>
        {
            var result = new ProcessResult { ExitCode = 128 };
            result.AppendStderr("object unavailable");
            return Task.FromResult<ProcessResult?>(result);
        };
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
            _state.GetSubtreeAsync(_repo, snapshot.Tree, "custom", CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("object unavailable"));
    }

    private sealed class RecordingGit(string configuration) : IGitCommandHelper
    {
        private readonly IGitCommandHelper _git = new GitCommandHelper(NullLogger<GitCommandHelper>.Instance, Mock.Of<IRawOutputHelper>());
        public List<GitOptions> Calls { get; } = [];
        public Func<GitOptions, CancellationToken, Task<ProcessResult?>>? Intercept { get; set; }

        public async Task<ProcessResult> Run(GitOptions options, CancellationToken ct)
        {
            Calls.Add(options);
            if (Intercept != null)
            {
                var intercepted = await Intercept(options, ct);
                if (intercepted != null)
                {
                    return intercepted;
                }
            }
            var environment = new Dictionary<string, string>(options.EnvironmentVariables ?? new Dictionary<string, string>())
            {
                ["GIT_CONFIG_NOSYSTEM"] = "1",
                ["GIT_CONFIG_GLOBAL"] = configuration
            };
            var args = options.Command == ProcessOptions.CMD ? options.Args.Skip(2).ToArray() : options.Args.ToArray();
            return await _git.Run(new GitOptions(args, options.WorkingDirectory, environmentVariables: environment), ct);
        }
    }
}
