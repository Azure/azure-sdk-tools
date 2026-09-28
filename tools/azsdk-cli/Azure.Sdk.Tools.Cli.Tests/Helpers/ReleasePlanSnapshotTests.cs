// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Exact-snapshot checks with mocked Git and metadata emission; no external processes run.
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
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";
    private const string Metadata = "languages:\n  Python:\n    packageName: azure-mgmt-contoso\n    apiVersion: 2024-01-01\n    sdkType: management\n";
    private TempDirectory _directory = null!;
    private string _project = string.Empty;
    private Mock<IGitHelper> _git = null!;
    private Mock<INpxHelper> _npx = null!;
    private Mock<IProcessHelper> _process = null!;
    private TypeSpecHelper _helper = null!;
    private readonly List<string> _steps = [];
    private string? _output;

    [SetUp]
    public void Setup()
    {
        _directory = TempDirectory.Create("release-target-snapshot");
        _project = Directory.CreateDirectory(Path.Combine(_directory.DirectoryPath, "specification", "contoso")).FullName;
        File.WriteAllText(Path.Combine(_project, "tspconfig.yaml"), "azure-resource-provider-folder: ./resource-manager\n");
        File.WriteAllText(Path.Combine(_project, "main.tsp"), "namespace Contoso;\n");
        _steps.Clear();
        _output = null;
        _git = new Mock<IGitHelper>(MockBehavior.Strict);
        _git.Setup(g => g.VerifyCleanSnapshotAsync(It.IsAny<string>(), Sha, It.IsAny<CancellationToken>()))
            .Callback(() => _steps.Add("verify")).Returns(Task.CompletedTask);
        _process = new Mock<IProcessHelper>(MockBehavior.Strict);
        _npx = new Mock<INpxHelper>(MockBehavior.Strict);
        _helper = new TypeSpecHelper(_git.Object, _process.Object);
        Emit(Metadata);
    }

    [TearDown]
    public void Cleanup() => _directory.Dispose();

    [TestCase("main")]
    [TestCase("0123456")]
    [TestCase("g123456789abcdef0123456789abcdef01234567")]
    public void InvalidShaDoesNotAccessGit(string sha)
    {
        var commands = new Mock<IGitCommandHelper>(MockBehavior.Strict);
        var helper = new GitHelper(Mock.Of<IGitHubService>(), commands.Object, NullLogger<GitHelper>.Instance);
        Assert.ThrowsAsync<ArgumentException>(() => helper.VerifyCleanSnapshotAsync(_project, sha, default));
        commands.VerifyNoOtherCalls();
    }

    [TestCase(Sha, "", 0, true)]
    [TestCase("fedcba9876543210fedcba9876543210fedcba98", "", 0, false)]
    [TestCase(Sha, " M main.tsp", 0, false)]
    [TestCase(Sha, "?? new-directory/", 0, false)]
    [TestCase(Sha, "", 1, false)]
    public async Task GitChecksHeadAndCleanStatusWithoutChangingFiles(string head, string status, int exitCode, bool succeeds)
    {
        var calls = new List<string>();
        var commands = new Mock<IGitCommandHelper>(MockBehavior.Strict);
        commands.Setup(g => g.Run(It.IsAny<GitOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitOptions options, CancellationToken _) =>
            {
                var command = string.Join(" ", Arguments(options));
                calls.Add(command);
                return command switch
                {
                    "rev-parse --show-toplevel" => Result(_directory.DirectoryPath),
                    "rev-parse HEAD" => Result(head),
                    "status --porcelain --untracked-files=normal" => Result(status, exitCode),
                    _ => throw new AssertionException($"Unexpected Git command: {command}")
                };
            });
        var helper = new GitHelper(Mock.Of<IGitHubService>(), commands.Object, NullLogger<GitHelper>.Instance);
        if (succeeds)
        {
            await helper.VerifyCleanSnapshotAsync(_project, Sha, default);
        }
        else
        {
            Assert.ThrowsAsync<InvalidOperationException>(() => helper.VerifyCleanSnapshotAsync(_project, Sha, default));
        }
        Assert.That(calls, Is.EqualTo(head == Sha
            ? new[] { "rev-parse --show-toplevel", "rev-parse HEAD", "status --porcelain --untracked-files=normal" }
            : new[] { "rev-parse --show-toplevel", "rev-parse HEAD" }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SnapshotChecksBeforeAndAfterMetadataAndCleansExternalOutput(bool configPath)
    {
        var path = configPath ? Path.Combine(_project, "tspconfig.yaml") : _project;
        var project = await _helper.ValidateReleasePlanSnapshotAsync(path, Sha, _npx.Object, NullLogger.Instance, default);
        Assert.That(project.Packages.Single().ApiVersion, Is.EqualTo("2024-01-01"));
        Assert.That(project.Packages.Single().PackageName, Is.EqualTo("azure-mgmt-contoso"));
        Assert.That(_steps, Is.EqualTo(new[] { "verify", "metadata", "verify" }));
        Assert.That(_output, Does.Not.StartWith(_directory.DirectoryPath));
        Assert.That(Directory.Exists(_output), Is.False);
        var options = (NpxOptions)_npx.Invocations.Single().Arguments[0];
        Assert.That(Arguments(options), Does.Contain("@azure-tools/typespec-metadata"));
        Assert.That(options.WorkingDirectory, Is.EqualTo(_project));
        _process.VerifyNoOtherCalls();
    }

    [TestCase("", 0)]
    [TestCase("languages: {}", 0)]
    [TestCase("languages: [", 0)]
    [TestCase(Metadata, 1)]
    public void MissingOrFailedMetadataCannotConfirm(string metadata, int exitCode)
    {
        Emit(metadata, exitCode);
        Assert.ThrowsAsync<InvalidOperationException>(() => Validate());
        Assert.That(Directory.Exists(_output), Is.False);
    }

    [TestCase(1)]
    [TestCase(2)]
    public void SnapshotDriftStopsBeforeSave(int failedCheck)
    {
        var checks = 0;
        _git.Setup(g => g.VerifyCleanSnapshotAsync(_project, Sha, It.IsAny<CancellationToken>()))
            .Returns(() => ++checks == failedCheck ? Task.FromException(new InvalidOperationException("snapshot drift")) : Task.CompletedTask);
        Assert.ThrowsAsync<InvalidOperationException>(() => Validate());
        _npx.Verify(n => n.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(failedCheck - 1));
        Assert.That(Directory.Exists(_output), Is.False);
    }

    [Test]
    public void MetadataCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        _npx.Setup(n => n.Run(It.IsAny<NpxOptions>(), cancellation.Token)).Returns<NpxOptions, CancellationToken>((_, ct) =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<ProcessResult>(ct);
        });
        Assert.CatchAsync<OperationCanceledException>(() => Validate(cancellation.Token));
        _git.Verify(g => g.VerifyCleanSnapshotAsync(_project, Sha, cancellation.Token), Times.Once);
    }

    [TestCase("")]
    [TestCase("https://github.com/Azure/azure-rest-api-specs/tree/main/specification/contoso")]
    public void SnapshotRequiresLocalProject(string path)
    {
        Assert.ThrowsAsync<ArgumentException>(() => _helper.ValidateReleasePlanSnapshotAsync(path, Sha, _npx.Object, NullLogger.Instance, default));
        _git.VerifyNoOtherCalls();
        _npx.VerifyNoOtherCalls();
    }

    private Task<TypeSpecProject> Validate(CancellationToken ct = default) =>
        _helper.ValidateReleasePlanSnapshotAsync(_project, Sha, _npx.Object, NullLogger.Instance, ct);

    private void Emit(string metadata, int exitCode = 0) =>
        _npx.Setup(n => n.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync((NpxOptions options, CancellationToken _) =>
        {
            _steps.Add("metadata");
            _output = Arguments(options)[^1];
            if (metadata.Length > 0)
            {
                var output = Directory.CreateDirectory(Path.Combine(_output, "@azure-tools", "typespec-metadata")).FullName;
                File.WriteAllText(Path.Combine(output, "typespec-metadata.yaml"), metadata);
            }
            return Result("", exitCode);
        });

    private static ProcessResult Result(string stdout, int exitCode = 0)
    {
        var result = new ProcessResult { ExitCode = exitCode };
        result.AppendStdout(stdout);
        return result;
    }

    private static string[] Arguments(ProcessOptions options) =>
        (options.Command == ProcessOptions.CMD ? options.Args.Skip(2) : options.Args).ToArray();
}
