// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class TspClientHelperTests
{
    private string _root = null!;
    private string _package = null!;
    private string _prefix = null!;
    private Mock<INpmHelper> _npm = null!;
    private Mock<ITypeSpecHelper> _typeSpec = null!;
    private TspClientHelper _helper = null!;
    private readonly List<NpmOptions> _commands = [];

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"tsp-client-helper-{Guid.NewGuid():N}");
        _package = Path.Combine(_root, "sdk", "test", "package");
        _prefix = Path.Combine(_root, "eng", "common", "tsp-client");
        Directory.CreateDirectory(_package);
        Directory.CreateDirectory(_prefix);
        File.WriteAllText(Path.Combine(_package, "tsp-location.yaml"), "repo: pinned-repo\ncommit: pinned-commit\ndirectory: pinned-directory\n");
        WriteManifests();
        _commands.Clear();
        _npm = new();
        _npm.Setup(n => n.Run(It.IsAny<NpmOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NpmOptions options, CancellationToken ct) =>
            {
                _commands.Add(options);
                if (options.Args.Contains("ci"))
                {
                    InstallBinary();
                }
                return new ProcessResult();
            });
        var git = new Mock<IGitHelper>();
        git.Setup(g => g.DiscoverRepoRootAsync(_package, It.IsAny<CancellationToken>())).ReturnsAsync(_root);
        _typeSpec = new();
        _helper = new(_npm.Object, _typeSpec.Object, git.Object, NullLogger<TspClientHelper>.Instance);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public async Task MissingExecutable_InstallsLockedDependenciesBeforeGeneration()
    {
        var pinPath = Path.Combine(_package, "tsp-location.yaml");
        var pin = File.ReadAllBytes(pinPath);
        var result = await _helper.UpdateGenerationAsync(_package);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(_commands, Has.Count.EqualTo(2));
            Assert.That(_commands[0].Args, Does.Contain("ci").And.Contain("--prefix").And.Contain(_prefix));
            Assert.That(_commands[0].WorkingDirectory, Is.EqualTo(_prefix));
            Assert.That(_commands[1].WorkingDirectory, Is.EqualTo(_package));
            Assert.That(_commands[1].Args, Does.Contain("update").And.Not.Contain("exec").And.Not.Contain("--commit"));
            Assert.That(File.ReadAllBytes(pinPath), Is.EqualTo(pin));
        });
    }

    [Test]
    public async Task InstalledExecutable_PreservesExistingToolAndDoesNotReinstallOnRetry()
    {
        InstallBinary();
        await _helper.UpdateGenerationAsync(_package);
        await _helper.UpdateGenerationAsync(_package);
        Assert.That(_commands, Has.Count.EqualTo(2));
        Assert.That(_commands.All(c => c.Args.Contains("update") && !c.Args.Contains("ci") && !c.Args.Contains("exec")), Is.True);
    }

    [Test]
    public async Task ExplicitLocalSpecAndCommit_ArePassedThroughAfterInstallation()
    {
        var localSpec = Path.Combine(_root, "local spec");
        await _helper.UpdateGenerationAsync(_package, commitSha: "explicit-commit", localSpecRepoPath: localSpec);
        Assert.That(_commands.Last().Args,
            Does.Contain("--local-spec-repo").And.Contain(localSpec).And.Contain("--commit").And.Contain("explicit-commit"));
    }

    [Test]
    public async Task SpecsRepository_UsesItsOwnLockedManifest()
    {
        _typeSpec.Setup(t => t.IsRepoPathForSpecRepoAsync(_root, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _prefix = _root;
        WriteManifests();
        var result = await _helper.UpdateGenerationAsync(_package);
        Assert.That(result.IsSuccessful, Is.True);
        Assert.That(_commands.First().Args, Does.Contain(_root));
        Assert.That(_commands.Last().Prefix, Is.EqualTo(_root));
    }

    [TestCase("package.json")]
    [TestCase("package-lock.json")]
    public async Task MissingManifest_FailsWithoutUnpinnedFallback(string name)
    {
        File.Delete(Path.Combine(_prefix, name));
        var result = await _helper.UpdateGenerationAsync(_package);
        Assert.That(result.IsSuccessful, Is.False);
        Assert.That(result.ResponseError, Does.Contain("package.json and package-lock.json"));
        Assert.That(_commands, Is.Empty);
    }

    [TestCase(1)]
    [TestCase(0)]
    public async Task InstallFailureOrMissingInstalledBin_NeverRunsGeneration(int exitCode)
    {
        var process = new ProcessResult { ExitCode = exitCode };
        process.AppendStderr("install diagnostic");
        _npm.Setup(n => n.Run(It.IsAny<NpmOptions>(), It.IsAny<CancellationToken>()))
            .Callback<NpmOptions, CancellationToken>((options, _) => _commands.Add(options))
            .ReturnsAsync(process);
        var result = await _helper.UpdateGenerationAsync(_package);
        Assert.That(result.IsSuccessful, Is.False);
        Assert.That(result.ResponseError, Does.Contain("install diagnostic"));
        Assert.That(_commands, Has.Count.EqualTo(1));
        Assert.That(_commands.Single().Args, Does.Contain("ci").And.Not.Contain("exec"));
    }

    [Test]
    public void InstallationCancellation_PropagatesWithoutGeneration()
    {
        using var cts = new CancellationTokenSource();
        _npm.Setup(n => n.Run(It.IsAny<NpmOptions>(), cts.Token))
            .ReturnsAsync(() =>
            {
                cts.Cancel();
                return new ProcessResult { ExitCode = -1 };
            });
        Assert.ThrowsAsync<OperationCanceledException>(() => _helper.UpdateGenerationAsync(_package, ct: cts.Token));
        _npm.Verify(n => n.Run(It.IsAny<NpmOptions>(), cts.Token), Times.Once);
    }

    [Test]
    public void GenerationCancellation_IsNotReportedAsOrdinaryGenerationFailure()
    {
        InstallBinary();
        using var cts = new CancellationTokenSource();
        _npm.Setup(n => n.Run(It.IsAny<NpmOptions>(), cts.Token))
            .ReturnsAsync(() =>
            {
                cts.Cancel();
                return new ProcessResult { ExitCode = -1 };
            });
        Assert.ThrowsAsync<OperationCanceledException>(() => _helper.UpdateGenerationAsync(_package, ct: cts.Token));
        _npm.Verify(n => n.Run(It.IsAny<NpmOptions>(), cts.Token), Times.Once);
    }

    [Test]
    public async Task MissingPin_DoesNotInstallOrGenerate()
    {
        File.Delete(Path.Combine(_package, "tsp-location.yaml"));
        var result = await _helper.UpdateGenerationAsync(_package);
        Assert.That(result.ResponseError, Does.Contain("tsp-location.yaml not found"));
        Assert.That(_commands, Is.Empty);
    }

    [Test]
    public async Task GenerationFailure_PreservesDiagnosticsAfterSuccessfulInstall()
    {
        var process = new ProcessResult { ExitCode = 2 };
        process.AppendStderr("generation diagnostic");
        _npm.Setup(n => n.Run(It.Is<NpmOptions>(o => o.Args.Contains("update")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);
        var result = await _helper.UpdateGenerationAsync(_package);
        Assert.That(result.IsSuccessful, Is.False);
        Assert.That(result.ResponseError, Does.Contain("generation diagnostic"));
    }

    private void WriteManifests()
    {
        File.WriteAllText(Path.Combine(_prefix, "package.json"),
            """{"dependencies":{"@azure-tools/typespec-client-generator-cli":"0.33.1"}}""");
        File.WriteAllText(Path.Combine(_prefix, "package-lock.json"), "{}");
    }

    private void InstallBinary()
    {
        var bin = Path.Combine(_prefix, "node_modules", ".bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, OperatingSystem.IsWindows() ? "tsp-client.cmd" : "tsp-client"), "mock executable");
    }
}
