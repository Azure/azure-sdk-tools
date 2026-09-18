// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Services.Repair;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Repair;

[TestFixture]
public class RepairArtifactsTests
{
    private RepairTestDirectory _directory = null!;
    private RepairArtifacts _artifacts = null!;
    private string _sdk = null!;
    private string _spec = null!;
    private string _output = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = new RepairTestDirectory();
        _artifacts = new RepairArtifacts();
        _sdk = _directory.Create("sdk");
        _spec = _directory.Create("spec");
        _output = Path.Combine(_directory.Root, "evidence");
    }

    [TearDown]
    public void TearDown() => _directory.Dispose();

    private string Create() => _artifacts.CreateDirectory(_output, "session", _sdk, _spec);

    [Test]
    public async Task CreateAndWrite_PersistsReadableAttributedJsonAndExactText()
    {
        var path = Create();
        await _artifacts.WriteJsonAsync(path, "result.json", new Receipt("validated"), CancellationToken.None);
        await _artifacts.WriteTextAsync(path, "attempt-1/build.log", "line 1\r\nline 2\nΩ", CancellationToken.None);

        var json = await File.ReadAllTextAsync(Path.Combine(path, "result.json"));
        Assert.That(json, Does.Contain("\n"));
        Assert.That(JsonDocument.Parse(json).RootElement.GetProperty("terminalReason").GetString(), Is.EqualTo("validated"));
        Assert.That(await File.ReadAllTextAsync(Path.Combine(path, "attempt-1", "build.log")), Is.EqualTo("line 1\r\nline 2\nΩ"));
        Assert.That(Directory.EnumerateFiles(path, ".repair-write-*", SearchOption.AllDirectories), Is.Empty);
    }

    [Test]
    public async Task WriteText_ReplacesCheckpointWithoutLeavingIntermediateFiles()
    {
        var path = Create();
        await _artifacts.WriteTextAsync(path, "result.json", "old checkpoint", CancellationToken.None);
        await _artifacts.WriteTextAsync(path, "result.json", "new checkpoint", CancellationToken.None);

        Assert.That(File.ReadAllText(Path.Combine(path, "result.json")), Is.EqualTo("new checkpoint"));
        Assert.That(Directory.GetFileSystemEntries(path).Select(Path.GetFileName), Is.EqualTo(new[] { "result.json" }));
    }

    [Test]
    public void Create_AcceptsEmptyDirectoryButRejectsReuse()
    {
        Directory.CreateDirectory(_output);
        Assert.That(Create(), Is.EqualTo(_output));
        Assert.Throws<IOException>(() => Create());
    }

    [Test]
    public void Create_RejectsNonemptyDirectoryWithoutTouchingExistingEvidence()
    {
        Directory.CreateDirectory(_output);
        var existing = Path.Combine(_output, "result.json");
        File.WriteAllText(existing, "prior evidence");
        Assert.Throws<IOException>(() => Create());
        Assert.That(File.ReadAllText(existing), Is.EqualTo("prior evidence"));
    }

    [Test]
    public void Create_RejectsExistingFile()
    {
        File.WriteAllText(_output, "existing");
        Assert.Throws<IOException>(() => Create());
    }

    [TestCase("sdk")]
    [TestCase("spec")]
    public void Create_RejectsSourceRootAndDescendants(string source)
    {
        var root = source == "sdk" ? _sdk : _spec;
        foreach (var path in new[] { root, Path.Combine(root, "evidence") })
        {
            Assert.Throws<ArgumentException>(() => _artifacts.CreateDirectory(path, "session", _sdk, _spec));
        }
        Assert.That(Directory.GetFileSystemEntries(root), Is.Empty);
    }

    [Test]
    public void Create_RejectsSourceAncestor()
    {
        Assert.Throws<ArgumentException>(() => _artifacts.CreateDirectory(_directory.Root, "session", _sdk, _spec));
    }

    [Test]
    public void Create_AllowsSiblingWithCommonPrefix()
    {
        var path = Path.Combine(_directory.Root, "sdk-evidence");
        Assert.That(_artifacts.CreateDirectory(path, "session", _sdk, null), Is.EqualTo(path));
    }

    [Test]
    public void Create_RejectsRelativePathAndMissingSource()
    {
        Assert.Throws<ArgumentException>(() => _artifacts.CreateDirectory("evidence", "session", _sdk, null));
        Assert.Throws<DirectoryNotFoundException>(() =>
            _artifacts.CreateDirectory(_output, "session", Path.Combine(_directory.Root, "missing"), null));
    }

    [TestCase("evidence.")]
    [TestCase("evidence ")]
    [TestCase("evidence&other")]
    [TestCase("evidence%VARIABLE%")]
    public void Create_RejectsWindowsAliasesAndShellMetacharacters(string name)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("These path aliases and command-shell characters are Windows-specific.");
        }
        Assert.Throws<ArgumentException>(() =>
            _artifacts.CreateDirectory(Path.Combine(_directory.Root, name), "session", _sdk, _spec));
    }

    [TestCase("../escape")]
    [TestCase("a/b")]
    [TestCase("a\\b")]
    [TestCase(".")]
    [TestCase("..")]
    public void Create_RejectsUnsafeSessionId(string session)
    {
        Assert.Throws<ArgumentException>(() => _artifacts.CreateDirectory(_output, session, _sdk, _spec));
    }

    [TestCase("../escape.txt")]
    [TestCase("..\\escape.txt")]
    [TestCase("attempt/../../escape.txt")]
    [TestCase("/absolute.txt")]
    [TestCase("\\absolute.txt")]
    [TestCase("C:\\absolute.txt")]
    [TestCase("result.json:stream")]
    [TestCase("attempt//log.txt")]
    [TestCase("./result.json")]
    [TestCase("NUL")]
    [TestCase("result.json.")]
    [TestCase("result.json ")]
    public void Write_RejectsUnsafeRelativePaths(string relativePath)
    {
        var path = Create();
        Assert.ThrowsAsync<ArgumentException>(() => _artifacts.WriteTextAsync(path, relativePath, "unsafe", CancellationToken.None));
        Assert.That(Directory.GetFileSystemEntries(path), Is.Empty);
    }

    [Test]
    public void Write_RejectsUnregisteredDirectoryIncludingSource()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _artifacts.WriteTextAsync(_sdk, "source.cs", "unsafe", CancellationToken.None));
        Assert.That(Directory.GetFileSystemEntries(_sdk), Is.Empty);
    }

    [Test]
    public async Task Write_CancellationPreservesPreviousCheckpoint()
    {
        var path = Create();
        await _artifacts.WriteTextAsync(path, "result.json", "previous", CancellationToken.None);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => _artifacts.WriteTextAsync(path, "result.json", "partial", cts.Token));
        Assert.That(File.ReadAllText(Path.Combine(path, "result.json")), Is.EqualTo("previous"));
        Assert.That(Directory.GetFileSystemEntries(path).Length, Is.EqualTo(1));
    }

    [Test]
    public async Task Write_SerializationFailurePreservesPreviousCheckpoint()
    {
        var path = Create();
        await _artifacts.WriteTextAsync(path, "result.json", "previous", CancellationToken.None);
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _artifacts.WriteJsonAsync(path, "result.json", new InvalidReceipt(), CancellationToken.None));
        Assert.That(File.ReadAllText(Path.Combine(path, "result.json")), Is.EqualTo("previous"));
        Assert.That(Directory.GetFileSystemEntries(path).Length, Is.EqualTo(1));
    }

    [Test]
    public void Write_FailedAtomicMoveSurfacesErrorAndCleansOwnedFile()
    {
        var path = Create();
        Directory.CreateDirectory(Path.Combine(path, "result.json"));
        var exception = Assert.CatchAsync(() => _artifacts.WriteTextAsync(path, "result.json", "partial", CancellationToken.None));
        Assert.That(exception, Is.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());
        Assert.That(Directory.GetFileSystemEntries(path).Select(Path.GetFileName), Is.EqualTo(new[] { "result.json" }));
    }

    [Test]
    public void Create_RejectsSymbolicLinkToSourceAndExternalDirectory()
    {
        var external = _directory.Create("external");
        var link = Path.Combine(_directory.Root, "link");
        CreateDirectoryLinkOrIgnore(link, external);
        Assert.Throws<InvalidOperationException>(() =>
            _artifacts.CreateDirectory(Path.Combine(link, "evidence"), "session", _sdk, _spec));
        Assert.That(Directory.GetFileSystemEntries(external), Is.Empty);
    }

    [Test]
    public void Create_RejectsSymlinkedSourceRoot()
    {
        var link = Path.Combine(_directory.Root, "sdk-link");
        CreateDirectoryLinkOrIgnore(link, _sdk);
        Assert.Throws<InvalidOperationException>(() => _artifacts.CreateDirectory(_output, "session", link, _spec));
    }

    [Test]
    public void Write_RejectsDirectorySymlinkCreatedAfterRegistration()
    {
        var path = Create();
        CreateDirectoryLinkOrIgnore(Path.Combine(path, "attempt"), _sdk);
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _artifacts.WriteTextAsync(path, "attempt/source.cs", "unsafe", CancellationToken.None));
        Assert.That(Directory.GetFileSystemEntries(_sdk), Is.Empty);
    }

    [Test]
    public void Write_RejectsArtifactRootReplacedByLink()
    {
        var path = Create();
        Directory.Delete(path);
        CreateDirectoryLinkOrIgnore(path, _sdk);
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _artifacts.WriteTextAsync(path, "source.cs", "unsafe", CancellationToken.None));
        Assert.That(Directory.GetFileSystemEntries(_sdk), Is.Empty);
    }

    [Test]
    public void Write_RejectsFileLinkWithoutChangingTarget()
    {
        var path = Create();
        var source = Path.Combine(_sdk, "source.cs");
        File.WriteAllText(source, "original");
        try
        {
            File.CreateSymbolicLink(Path.Combine(path, "result.json"), source);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            Assert.Ignore($"File symbolic links are unavailable: {ex.Message}");
        }
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _artifacts.WriteTextAsync(path, "result.json", "unsafe", CancellationToken.None));
        Assert.That(File.ReadAllText(source), Is.EqualTo("original"));
    }

    private static void CreateDirectoryLinkOrIgnore(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            Assert.Ignore($"Directory symbolic links are unavailable: {ex.Message}");
        }
    }

    private record Receipt([property: JsonPropertyName("terminalReason")] string Reason);

    private class InvalidReceipt
    {
        public string Value => throw new InvalidOperationException("Serialization failed.");
    }
}
