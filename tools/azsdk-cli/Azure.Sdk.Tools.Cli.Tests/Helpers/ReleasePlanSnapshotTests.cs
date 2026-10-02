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
    private string? _output;

    [SetUp]
    public void Setup()
    {
        _directory = TempDirectory.Create("release-target-snapshot");
        _project = Directory.CreateDirectory(Path.Combine(_directory.DirectoryPath, "specification", "contoso")).FullName;
        File.WriteAllText(Path.Combine(_project, "tspconfig.yaml"), "azure-resource-provider-folder: ./resource-manager\n");
        File.WriteAllText(Path.Combine(_project, "main.tsp"), "namespace Contoso;\n");
        _output = null;
        _git = new Mock<IGitHelper>(MockBehavior.Strict);
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
    public async Task ParseEmitsMetadataAndCleansExternalOutput(bool configPath)
    {
        var path = configPath ? Path.Combine(_project, "tspconfig.yaml") : _project;
        var project = await _helper.ParseTypeSpecProjectAsync(path, _npx.Object, NullLogger.Instance, default);
        Assert.That(project, Is.Not.Null);
        Assert.That(project!.Packages.Single().ApiVersion, Is.EqualTo("2024-01-01"));
        Assert.That(project.Packages.Single().PackageName, Is.EqualTo("azure-mgmt-contoso"));
        Assert.That(_output, Is.Not.Null);
        Assert.That(_output, Does.Not.StartWith(_directory.DirectoryPath));
        Assert.That(Directory.Exists(_output), Is.False);
        var options = (NpxOptions)_npx.Invocations.Single().Arguments[0];
        Assert.That(Arguments(options), Does.Contain("@azure-tools/typespec-metadata"));
        Assert.That(options.WorkingDirectory, Is.EqualTo(_project));
        _git.VerifyNoOtherCalls();
        _process.VerifyNoOtherCalls();
    }

    [TestCase("", 0)]
    [TestCase("languages: [", 0)]
    [TestCase(Metadata, 1)]
    public async Task MissingOrFailedMetadataCannotBeMistakenForAnUnversionedProject(string metadata, int exitCode)
    {
        Emit(metadata, exitCode);
        var project = await Parse();
        Assert.That(project, Is.Null);
        Assert.That(_output, Is.Not.Null);
        Assert.That(Directory.Exists(_output), Is.False);
        _git.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ValidMetadataWithoutEmittersReturnsAnUnversionedProject()
    {
        Emit("languages: {}", 0);
        var project = await Parse();
        Assert.That(project, Is.Not.Null);
        Assert.That(project!.Packages, Is.Empty);
        Assert.That(Directory.Exists(_output), Is.False);
    }

    [Test]
    public void MetadataCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        _npx.Setup(n => n.Run(It.IsAny<NpxOptions>(), cancellation.Token)).Returns<NpxOptions, CancellationToken>((options, ct) =>
        {
            var outputIndex = options.Args.IndexOf("--output-dir");
            Assert.That(outputIndex, Is.GreaterThanOrEqualTo(0));
            _output = options.Args[outputIndex + 1];
            Assert.That(Path.GetDirectoryName(_output), Is.EqualTo(Path.TrimEndingDirectorySeparator(Path.GetTempPath())));
            Assert.That(Path.GetFileName(_output), Does.Match("^azsdk-spec-metadata-[0-9a-f]{32}$"));
            Directory.CreateDirectory(_output);
            cancellation.Cancel();
            return Task.FromCanceled<ProcessResult>(ct);
        });
        Assert.CatchAsync<OperationCanceledException>(() => Parse(cancellation.Token));
        Assert.That(_output, Is.Not.Null);
        Assert.That(Directory.Exists(_output), Is.False);
        _npx.Verify(n => n.Run(It.IsAny<NpxOptions>(), cancellation.Token), Times.Once);
        _git.VerifyNoOtherCalls();
    }

    [TestCase("")]
    [TestCase("https://github.com/Azure/azure-rest-api-specs/tree/main/specification/contoso")]
    public async Task ParseReturnsNullForInvalidProject(string path)
    {
        var project = await _helper.ParseTypeSpecProjectAsync(path, _npx.Object, NullLogger.Instance, default);
        Assert.That(project, Is.Null);
        _git.VerifyNoOtherCalls();
        _npx.VerifyNoOtherCalls();
    }

    private Task<TypeSpecProject?> Parse(CancellationToken ct = default) =>
        _helper.ParseTypeSpecProjectAsync(_project, _npx.Object, NullLogger.Instance, ct);

    private void Emit(string metadata, int exitCode = 0) =>
        _npx.Setup(n => n.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync((NpxOptions options, CancellationToken _) =>
        {
            var outputIndex = options.Args.IndexOf("--output-dir");
            Assert.That(outputIndex, Is.GreaterThanOrEqualTo(0));
            _output = options.Args[outputIndex + 1];
            Assert.That(Path.GetDirectoryName(_output), Is.EqualTo(Path.TrimEndingDirectorySeparator(Path.GetTempPath())));
            Assert.That(Path.GetFileName(_output), Does.Match("^azsdk-spec-metadata-[0-9a-f]{32}$"));
            var output = Directory.CreateDirectory(Path.Combine(_output, "@azure-tools", "typespec-metadata")).FullName;
            if (metadata.Length > 0)
            {
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
