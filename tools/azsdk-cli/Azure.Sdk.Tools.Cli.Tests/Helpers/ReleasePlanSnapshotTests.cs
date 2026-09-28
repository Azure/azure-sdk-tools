// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Tests exact-snapshot validation without running Git, npm, npx, or the TypeSpec compiler.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
internal class ReleasePlanSnapshotTests
{
    private const string SpecCommitSha = "0123456789abcdef0123456789abcdef01234567";
    private const string OtherCommitSha = "fedcba9876543210fedcba9876543210fedcba98";
    private const string PreviewApiVersion = "2021-10-01-preview";
    private const string DiscoverCommand = "rev-parse --show-toplevel";
    private const string HeadCommand = "rev-parse HEAD";
    private const string StatusCommand = "status --porcelain --untracked-files=normal";
    private const string MetadataYaml = """
        languages:
          csharp:
            packageName: Azure.ResourceManager.Contoso
            apiVersion: 2021-10-01-preview
            sdkType: management
          Python:
            packageName: azure-mgmt-contoso
            apiVersion: 2021-10-01-preview
            sdkType: management
        """;

    private static readonly string[] SnapshotCommands = [DiscoverCommand, HeadCommand, StatusCommand];

    private CancellationTokenSource _cancellation = null!;
    private Mock<IGitCommandHelper> _gitCommands = null!;
    private Mock<IGitHubService> _github = null!;
    private GitHelper _gitHelper = null!;
    private Mock<IGitHelper> _snapshotGit = null!;
    private Mock<IProcessHelper> _process = null!;
    private Mock<INpxHelper> _npx = null!;
    private TypeSpecHelper _typeSpecHelper = null!;
    private TempDirectory? _fixtureDirectory;
    private string _projectPath = string.Empty;
    private string _validationPath = string.Empty;
    private List<string> _steps = [];

    [SetUp]
    public void Setup()
    {
        _cancellation = new CancellationTokenSource();
        _gitCommands = new Mock<IGitCommandHelper>(MockBehavior.Strict);
        _github = new Mock<IGitHubService>(MockBehavior.Strict);
        _gitHelper = new GitHelper(_github.Object, _gitCommands.Object, NullLogger<GitHelper>.Instance);
        _snapshotGit = new Mock<IGitHelper>(MockBehavior.Strict);
        _process = new Mock<IProcessHelper>(MockBehavior.Strict);
        _npx = new Mock<INpxHelper>(MockBehavior.Strict);
        _typeSpecHelper = new TypeSpecHelper(_snapshotGit.Object, _process.Object);
        _fixtureDirectory = null;
        _projectPath = string.Empty;
        _validationPath = string.Empty;
        _steps = [];
    }

    [TearDown]
    public void TearDown()
    {
        _fixtureDirectory?.Dispose();
        _cancellation.Dispose();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("HEAD")]
    [TestCase("main")]
    [TestCase("0123456")]
    [TestCase("0123456789abcdef0123456789abcdef0123456")]
    [TestCase(SpecCommitSha + "0")]
    [TestCase("g123456789abcdef0123456789abcdef01234567")]
    [TestCase(" " + SpecCommitSha)]
    [TestCase(SpecCommitSha + "\n")]
    public void VerifyCleanSnapshotAsync_MalformedShaFailsBeforeRepositoryAccess(string? commitSha)
    {
        // An unusable path makes an accidental attempt at repository discovery fail differently.
        var error = Assert.ThrowsAsync<ArgumentException>(() =>
            _gitHelper.VerifyCleanSnapshotAsync(null!, commitSha!, _cancellation.Token));

        Assert.That(error!.ParamName, Is.EqualTo("commitSha"));
        Assert.That(error.Message, Does.Contain("40-character"));
        AssertGitCommands();
    }

    [TestCase(false, "")]
    [TestCase(true, " \t\r\n")]
    public async Task VerifyCleanSnapshotAsync_CleanMatchingHeadChecksHeadAndStatus(bool uppercaseHead, string status)
    {
        using var repository = TempDirectory.Create("snapshot git");
        var projectPath = Directory.CreateDirectory(Path.Combine(repository.DirectoryPath, "specification", "project")).FullName;
        var head = uppercaseHead ? SpecCommitSha.ToUpperInvariant() : SpecCommitSha;
        SetupGitCommands(repository.DirectoryPath, $" {head}\r\n", status);

        await _gitHelper.VerifyCleanSnapshotAsync(projectPath, SpecCommitSha, _cancellation.Token);

        AssertGitCommands(SnapshotCommands);
        var options = _gitCommands.Invocations.Select(call => (GitOptions)call.Arguments[0]).ToList();
        Assert.That(options[0].WorkingDirectory, Is.EqualTo(projectPath));
        Assert.That(options.Skip(1).Select(option => option.WorkingDirectory),
            Is.All.EqualTo(repository.DirectoryPath), "HEAD and status must be inspected at the discovered repository root.");
    }

    [Test]
    public void VerifyCleanSnapshotAsync_WrongHeadFailsWithoutCheckingStatusOrSwitchingFiles()
    {
        using var repository = TempDirectory.Create("snapshot-git");
        SetupGitCommands(repository.DirectoryPath, OtherCommitSha);

        var error = Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gitHelper.VerifyCleanSnapshotAsync(repository.DirectoryPath, SpecCommitSha, _cancellation.Token));

        Assert.That(error!.Message, Does.Contain(SpecCommitSha).And.Contain("different HEAD"));
        AssertGitCommands(DiscoverCommand, HeadCommand);
    }

    [TestCase(" M tracked.txt")]
    [TestCase("M  tracked.txt")]
    [TestCase("?? untracked.txt")]
    [TestCase("?? new-directory/")]
    public void VerifyCleanSnapshotAsync_DirtyOrUntrackedFilesFailWithoutRepairingCheckout(string status)
    {
        using var repository = TempDirectory.Create("snapshot-git");
        SetupGitCommands(repository.DirectoryPath, status: status);

        var error = Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gitHelper.VerifyCleanSnapshotAsync(repository.DirectoryPath, SpecCommitSha, _cancellation.Token));

        Assert.That(error!.Message, Does.Contain("clean checkout"));
        AssertGitCommands(SnapshotCommands);
    }

    [TestCase(DiscoverCommand)]
    [TestCase(HeadCommand)]
    [TestCase(StatusCommand)]
    public void VerifyCleanSnapshotAsync_NonzeroGitExitStopsValidation(string failedCommand)
    {
        using var repository = TempDirectory.Create("snapshot-git");
        var results = SetupGitCommands(repository.DirectoryPath);
        // Leave the otherwise-valid stdout intact: a failed command must not be treated as success.
        results[failedCommand].ExitCode = 1;
        results[failedCommand].AppendStderr("git failed");

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gitHelper.VerifyCleanSnapshotAsync(repository.DirectoryPath, SpecCommitSha, _cancellation.Token));

        AssertGitCommands(CommandsThrough(failedCommand));
    }

    [TestCase(DiscoverCommand)]
    [TestCase(HeadCommand)]
    [TestCase(StatusCommand)]
    public void VerifyCleanSnapshotAsync_GitProcessFailurePropagates(string failedCommand)
    {
        using var repository = TempDirectory.Create("snapshot-git");
        SetupGitCommands(repository.DirectoryPath);
        var failure = new IOException("git could not start");
        _gitCommands.Setup(helper => helper.Run(
                It.Is<GitOptions>(options => string.Join(" ", GetArguments(options)) == failedCommand), _cancellation.Token))
            .ThrowsAsync(failure);

        var error = Assert.ThrowsAsync<IOException>(() =>
            _gitHelper.VerifyCleanSnapshotAsync(repository.DirectoryPath, SpecCommitSha, _cancellation.Token));

        Assert.That(error, Is.SameAs(failure));
        AssertGitCommands(CommandsThrough(failedCommand));
    }

    [TestCase(DiscoverCommand)]
    [TestCase(HeadCommand)]
    [TestCase(StatusCommand)]
    public void VerifyCleanSnapshotAsync_CallerCancellationPropagates(string canceledCommand)
    {
        using var repository = TempDirectory.Create("snapshot-git");
        SetupGitCommands(repository.DirectoryPath);
        _gitCommands.Setup(helper => helper.Run(
                It.Is<GitOptions>(options => string.Join(" ", GetArguments(options)) == canceledCommand), _cancellation.Token))
            .Returns<GitOptions, CancellationToken>((_, ct) =>
            {
                _cancellation.Cancel();
                return Task.FromCanceled<ProcessResult>(ct);
            });

        var error = Assert.CatchAsync<OperationCanceledException>(() =>
            _gitHelper.VerifyCleanSnapshotAsync(repository.DirectoryPath, SpecCommitSha, _cancellation.Token));

        Assert.That(error!.CancellationToken, Is.EqualTo(_cancellation.Token));
        AssertGitCommands(CommandsThrough(canceledCommand));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ValidateReleasePlanSnapshotAsync_UsesMetadataAndChecksSameCommitBeforeAndAfter(bool useConfigPath)
    {
        CreateTypeSpecFixture(useConfigPath);

        var project = await ValidateSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(project.ProjectRootPath, Is.EqualTo(_projectPath));
            Assert.That(project.IsManagementPlane, Is.True);
            Assert.That(project.Packages.Select(package => package.PackageName),
                Is.EquivalentTo(new[] { "Azure.ResourceManager.Contoso", "azure-mgmt-contoso" }));
            Assert.That(project.Packages.Select(package => package.ApiVersion), Is.All.EqualTo(PreviewApiVersion));
            Assert.That(_steps, Is.EqualTo(new[] { "verify", "metadata", "verify" }));
        });
        AssertSnapshotCalls(2, 1);

        var emitter = (NpxOptions)_npx.Invocations.Single().Arguments[0];
        Assert.That(emitter.Package, Is.EqualTo("@typespec/compiler"));
        Assert.That(emitter.WorkingDirectory, Is.EqualTo(_projectPath));
        Assert.That(GetArguments(emitter), Is.EqualTo(new[]
        {
            "--yes", "--package=@typespec/compiler", "--", "tsp", "compile", ".",
            "--emit", "@azure-tools/typespec-metadata", "--output-dir", GetArguments(emitter)[^1]
        }));
        Assert.That(GetArguments(emitter)[^1], Does.Contain("azsdk-spec-metadata-"));
        Assert.That(GetArguments(emitter)[^1], Does.Not.StartWith(_projectPath));
        Assert.That(Directory.Exists(GetArguments(emitter)[^1]), Is.False, "Temporary metadata must be removed after validation.");
    }

    [TestCase("")]
    [TestCase("https://github.com/Azure/azure-rest-api-specs/tree/main/specification/testcontoso/Contoso.Management")]
    public void ValidateReleasePlanSnapshotAsync_RequiresLocalProjectBeforeGitOrProcesses(string projectPath)
    {
        var error = Assert.ThrowsAsync<ArgumentException>(() => _typeSpecHelper.ValidateReleasePlanSnapshotAsync(
            projectPath, SpecCommitSha, _npx.Object, NullLogger<TypeSpecHelper>.Instance, _cancellation.Token));

        Assert.That(error!.Message, Does.Contain("local TypeSpec project"));
        AssertSnapshotCalls(0, 0);
    }

    [Test]
    public void ValidateReleasePlanSnapshotAsync_InitialSnapshotFailureDoesNotCompile()
    {
        CreateTypeSpecFixture();
        var failure = new InvalidOperationException("HEAD does not match the selected snapshot");
        _snapshotGit.Setup(helper => helper.VerifyCleanSnapshotAsync(_validationPath, SpecCommitSha, _cancellation.Token))
            .ThrowsAsync(failure);

        var error = Assert.ThrowsAsync<InvalidOperationException>(() => ValidateSnapshot());

        Assert.That(error, Is.SameAs(failure));
        AssertSnapshotCalls(1, 0);
    }

    [Test]
    public void ValidateReleasePlanSnapshotAsync_MissingEntrypointDoesNotReturnAProject()
    {
        CreateTypeSpecFixture();
        File.Delete(Path.Combine(_projectPath, "main.tsp"));

        Assert.ThrowsAsync<InvalidOperationException>(() => ValidateSnapshot());

        AssertSnapshotCalls(1, 0);
    }

    [TestCase("failed")]
    [TestCase("missing")]
    [TestCase("empty")]
    [TestCase("malformed")]
    public void ValidateReleasePlanSnapshotAsync_MetadataWithoutPackagesCannotConfirmASnapshot(string emission)
    {
        CreateTypeSpecFixture();
        var metadata = emission switch
        {
            "failed" => MetadataYaml,
            "missing" => null,
            "empty" => "languages: {}",
            "malformed" => "languages: [",
            _ => throw new ArgumentException("Unknown metadata test case.", nameof(emission))
        };
        SetupMetadataEmitter(emission == "failed" ? 1 : 0, metadata);

        // Regression: ParseTypeSpecProjectAsync can return a project with no packages after emitter failure.
        Assert.ThrowsAsync<InvalidOperationException>(() => ValidateSnapshot());

        AssertSnapshotCalls(1, 1);
    }

    [TestCase("HEAD changed during compilation")]
    [TestCase("Untracked files appeared during compilation")]
    public void ValidateReleasePlanSnapshotAsync_FinalSnapshotDriftDoesNotReturnAProject(string message)
    {
        CreateTypeSpecFixture();
        var failure = new InvalidOperationException(message);
        _snapshotGit.SetupSequence(helper => helper.VerifyCleanSnapshotAsync(_validationPath, SpecCommitSha, _cancellation.Token))
            .Returns(Task.CompletedTask)
            .ThrowsAsync(failure);
        TypeSpecProject? project = null;

        var error = Assert.ThrowsAsync<InvalidOperationException>(async () => project = await ValidateSnapshot());

        Assert.That(error, Is.SameAs(failure));
        Assert.That(project, Is.Null);
        AssertSnapshotCalls(2, 1);
    }

    [Test]
    public void ValidateReleasePlanSnapshotAsync_MetadataCancellationDoesNotReturnAProject()
    {
        CreateTypeSpecFixture();
        _npx.Setup(helper => helper.Run(It.IsAny<NpxOptions>(), _cancellation.Token))
            .Returns<NpxOptions, CancellationToken>((_, ct) =>
            {
                _cancellation.Cancel();
                return Task.FromCanceled<ProcessResult>(ct);
            });
        TypeSpecProject? project = null;

        var error = Assert.CatchAsync<OperationCanceledException>(async () => project = await ValidateSnapshot());

        Assert.That(error!.CancellationToken, Is.EqualTo(_cancellation.Token));
        Assert.That(project, Is.Null);
        AssertSnapshotCalls(1, 1);
    }

    [TestCase(1)]
    [TestCase(2)]
    public void ValidateReleasePlanSnapshotAsync_SnapshotCheckCancellationPropagates(int canceledCheck)
    {
        CreateTypeSpecFixture();
        var checks = 0;
        _snapshotGit.Setup(helper => helper.VerifyCleanSnapshotAsync(_validationPath, SpecCommitSha, _cancellation.Token))
            .Returns<string, string, CancellationToken>((_, _, ct) =>
            {
                if (++checks == canceledCheck)
                {
                    _cancellation.Cancel();
                    return Task.FromCanceled(ct);
                }
                return Task.CompletedTask;
            });

        var error = Assert.CatchAsync<OperationCanceledException>(() => ValidateSnapshot());

        Assert.That(error!.CancellationToken, Is.EqualTo(_cancellation.Token));
        AssertSnapshotCalls(canceledCheck, canceledCheck - 1);
    }

    private Dictionary<string, ProcessResult> SetupGitCommands(string repositoryRoot, string? head = null, string status = "")
    {
        var results = new Dictionary<string, ProcessResult>
        {
            [DiscoverCommand] = Result(repositoryRoot.Replace('\\', '/') + "\n"),
            [HeadCommand] = Result(head ?? SpecCommitSha),
            [StatusCommand] = Result(status)
        };
        _gitCommands.Setup(helper => helper.Run(It.IsAny<GitOptions>(), _cancellation.Token))
            .ReturnsAsync((GitOptions options, CancellationToken _) =>
            {
                var command = string.Join(" ", GetArguments(options));
                return results.TryGetValue(command, out var result)
                    ? result
                    : throw new AssertionException($"Unexpected Git command: {command}");
            });
        return results;
    }

    private void AssertGitCommands(params string[] expected)
    {
        Assert.That(_gitCommands.Invocations.Select(call => string.Join(" ", GetArguments((GitOptions)call.Arguments[0]))),
            Is.EqualTo(expected), "Snapshot validation must never reset, checkout, stash, or otherwise mutate the repository.");
        _gitCommands.Verify(helper => helper.Run(It.IsAny<GitOptions>(), _cancellation.Token), Times.Exactly(expected.Length));
        _gitCommands.VerifyNoOtherCalls();
        _github.VerifyNoOtherCalls();
    }

    private static string[] CommandsThrough(string command) =>
        SnapshotCommands.Take(Array.IndexOf(SnapshotCommands, command) + 1).ToArray();

    private void CreateTypeSpecFixture(bool useConfigPath = false)
    {
        _fixtureDirectory = TempDirectory.Create("release plan snapshot");
        _projectPath = Path.Combine(_fixtureDirectory.DirectoryPath, "specification", "testcontoso", "Contoso.Management");
        var source = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "TypeSpecTestData", "specification", "testcontoso", "Contoso.Management");
        Directory.CreateDirectory(_projectPath);
        foreach (var file in new[] { "tspconfig.yaml", "main.tsp", "employee.tsp" })
        {
            File.Copy(Path.Combine(source, file), Path.Combine(_projectPath, file));
        }
        _validationPath = useConfigPath ? Path.Combine(_projectPath, "tspconfig.yaml") : _projectPath;
        _snapshotGit.Setup(helper => helper.VerifyCleanSnapshotAsync(_validationPath, SpecCommitSha, _cancellation.Token))
            .Callback(() => _steps.Add("verify"))
            .Returns(Task.CompletedTask);
        SetupMetadataEmitter(0, MetadataYaml);
    }

    private void SetupMetadataEmitter(int exitCode, string? metadataYaml)
    {
        _npx.Setup(helper => helper.Run(It.IsAny<NpxOptions>(), _cancellation.Token))
            .Returns<NpxOptions, CancellationToken>(async (options, ct) =>
            {
                _steps.Add("metadata");
                if (metadataYaml != null)
                {
                    var output = Path.Combine(GetArguments(options)[^1], "@azure-tools", "typespec-metadata");
                    Directory.CreateDirectory(output);
                    await File.WriteAllTextAsync(Path.Combine(output, "typespec-metadata.yaml"), metadataYaml, ct);
                }
                return Result(exitCode: exitCode, stderr: exitCode == 0 ? null : "Metadata emitter failed.");
            });
    }

    private Task<TypeSpecProject> ValidateSnapshot() => _typeSpecHelper.ValidateReleasePlanSnapshotAsync(
        _validationPath, SpecCommitSha, _npx.Object, NullLogger<TypeSpecHelper>.Instance, _cancellation.Token);

    private void AssertSnapshotCalls(int snapshotChecks, int metadataRuns)
    {
        _snapshotGit.Verify(helper => helper.VerifyCleanSnapshotAsync(_validationPath, SpecCommitSha, _cancellation.Token), Times.Exactly(snapshotChecks));
        _npx.Verify(helper => helper.Run(It.IsAny<NpxOptions>(), _cancellation.Token), Times.Exactly(metadataRuns));
        _snapshotGit.VerifyNoOtherCalls();
        _npx.VerifyNoOtherCalls();
        _process.VerifyNoOtherCalls();
    }

    private static ProcessResult Result(string stdout = "", int exitCode = 0, string? stderr = null)
    {
        var result = new ProcessResult { ExitCode = exitCode };
        result.AppendStdout(stdout);
        if (stderr != null)
        {
            result.AppendStderr(stderr);
        }
        return result;
    }

    // ProcessOptions wraps commands with cmd.exe /C on Windows; assertions inspect the actual arguments.
    private static string[] GetArguments(ProcessOptions options) =>
        (options.Command == ProcessOptions.CMD ? options.Args.Skip(2) : options.Args).ToArray();
}