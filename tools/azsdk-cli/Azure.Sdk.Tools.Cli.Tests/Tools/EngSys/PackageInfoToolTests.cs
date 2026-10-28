// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.EngSys;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.EngSys;

[TestFixture]
public class PackageInfoToolTests
{
    private TempDirectory _directory = null!;
    private Mock<IGitHelper> _git = null!;
    private Mock<LanguageService> _languageService = null!;
    private Mock<IPackageInfoHelper> _helper = null!;
    private PackageInfoTool _tool = null!;
    private PackageInfo _package = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = TempDirectory.Create("PackageInfoToolTests");
        _git = new Mock<IGitHelper>(MockBehavior.Strict);
        _git.Setup(git => git.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-js");
        _languageService = new Mock<LanguageService>(MockBehavior.Strict);
        _languageService.SetupGet(service => service.Language).Returns(SdkLanguage.JavaScript);
        _package = new PackageInfo
        {
            PackageName = "@azure/example",
            PackageVersion = "1.0.0",
            ApiVersions = ["2025-01-01"],
            Language = SdkLanguage.JavaScript,
            PackagePath = _directory.DirectoryPath
        };
        _languageService.Setup(service => service.GetPackageInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_package);
        _helper = new Mock<IPackageInfoHelper>(MockBehavior.Strict);
        _tool = new PackageInfoTool(_git.Object, new TestLogger<PackageInfoTool>(), _helper.Object, [_languageService.Object]);
    }

    [TearDown]
    public void TearDown() => _directory.Dispose();

    [Test]
    public async Task SinglePackageReturnsPackageInfoWithoutExportOrBulkDiscovery()
    {
        var response = await Invoke("--package-path", _directory.DirectoryPath, "--output", "json");
        var result = (DefaultCommandResponse)response;
        using var json = JsonDocument.Parse(new OutputHelper(OutputHelper.OutputModes.Json).Format(response));

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(result.Result, Is.SameAs(_package));
        Assert.That(json.RootElement.GetProperty("result").GetProperty("Name").GetString(), Is.EqualTo("@azure/example"));
        Assert.That(json.RootElement.GetProperty("result").GetProperty("ApiVersions")[0].GetString(), Is.EqualTo("2025-01-01"));
        Assert.That(response.ToString(), Does.Contain("\"ApiVersions\""));
        _languageService.Verify(service => service.GetPackageInfo(_directory.DirectoryPath, It.IsAny<CancellationToken>()), Times.Once);
        _languageService.Verify(service => service.DiscoverPackagesAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _helper.VerifyNoOtherCalls();
    }

    [Test]
    public async Task SinglePackageResolvesRelativePathAndSupportsDevVersion()
    {
        var response = await Invoke("-p", Path.GetRelativePath(Environment.CurrentDirectory, _directory.DirectoryPath), "--add-dev-version");

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(_package.DevVersion, Is.EqualTo(_package.PackageVersion));
        _languageService.Verify(service => service.GetPackageInfo(_directory.DirectoryPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SinglePackageCanExportExistingPackageInfoFormat()
    {
        var outDir = Path.Combine(_directory.DirectoryPath, "out");
        _helper.Setup(helper => helper.WritePackageInfoFile(_package, Path.Combine(outDir, "@azure/example.json"), true));

        var response = await Invoke("--package-path", _directory.DirectoryPath, "--out-dir", outDir, "--add-dev-version");

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(((DefaultCommandResponse)response).Result,
            Is.EqualTo(new[] { Path.Combine(outDir, "@azure/example.json") }));
        _helper.VerifyAll();
    }

    [Test]
    public async Task SinglePackageMissingMetadataIsNotFailure()
    {
        _package.ApiVersions = [];
        var response = await Invoke("--package-path", _directory.DirectoryPath);

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(((DefaultCommandResponse)response).Result, Is.SameAs(_package));
    }

    [TestCase("--service-directory", "storage")]
    [TestCase("--artifact", "example")]
    [TestCase("--ci", "true")]
    [TestCase("--repo-root", "some-repo")]
    public async Task SinglePackageRejectsBulkOptions(string option, string value)
    {
        var response = await Invoke("--package-path", _directory.DirectoryPath, option, value);

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(response.ResponseError, Does.Contain("cannot be combined"));
        _git.VerifyNoOtherCalls();
        _languageService.Verify(service => service.GetPackageInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task BulkModeStillRequiresOutDir()
    {
        var response = await Invoke();

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(response.ResponseError, Does.Contain("--out-dir is required"));
        _git.VerifyNoOtherCalls();
    }

    [TestCase("")]
    [TestCase(" ")]
    public async Task RejectsEmptyPackagePath(string path)
    {
        var response = await Invoke("--package-path", path);

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(response.ResponseError, Does.Contain("--package-path cannot be empty"));
    }

    [Test]
    public async Task RejectsMissingPackageDirectory()
    {
        var response = await Invoke("--package-path", Path.Combine(_directory.DirectoryPath, "missing"));

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(response.ResponseError, Does.Contain("does not exist"));
        _git.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RejectsUnknownRepository()
    {
        _git.Setup(git => git.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("unknown");
        var response = await Invoke("--package-path", _directory.DirectoryPath);

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(response.ResponseError, Does.Contain("Unable to resolve language service"));
    }

    [Test]
    public async Task FailedPackageLookupReturnsError()
    {
        _languageService.Setup(service => service.GetPackageInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Package manifest cannot be read."));
        var response = await Invoke("--package-path", _directory.DirectoryPath);

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(response.ResponseError, Is.EqualTo("Package manifest cannot be read."));
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase(" ", false)]
    [TestCase(null, true)]
    [TestCase("", true)]
    [TestCase(" ", true)]
    public async Task SinglePackageRequiresPackageNameInBothOutputModes(string? packageName, bool export)
    {
        _package.PackageName = packageName;
        var response = await Invoke(export
            ? ["--package-path", _directory.DirectoryPath, "--out-dir", "out"]
            : ["--package-path", _directory.DirectoryPath]);

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(response.ResponseError, Does.Contain("package name could not be determined"));
        Assert.That(((DefaultCommandResponse)response).Result, Is.Null);
        _helper.VerifyNoOtherCalls();
    }

    [Test]
    public async Task BulkExportKeepsDiscoveryFilteringAndFileResponse()
    {
        var outDir = Path.Combine(_directory.DirectoryPath, "out");
        var packages = new List<PackageInfo> { _package };
        _languageService.Setup(service => service.DiscoverPackagesAsync(
            It.IsAny<string>(), "test-service", It.IsAny<CancellationToken>())).ReturnsAsync(packages);
        _helper.Setup(helper => helper.FilterPackagesByArtifact(
            It.Is<List<PackageInfo>>(items => items.Single() == _package),
            It.Is<string[]>(artifacts => artifacts.SequenceEqual(new[] { "example" })))).Returns(packages);
        _helper.Setup(helper => helper.WritePackageInfoFile(_package, Path.Combine(outDir, "@azure/example.json"), false));

        var response = await Invoke("--repo-root", _directory.DirectoryPath, "--out-dir", outDir,
            "--service-directory", "test-service", "--artifact", "example");

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(((DefaultCommandResponse)response).Result, Is.EqualTo(new[] { Path.Combine(outDir, "@azure/example.json") }));
        _helper.VerifyAll();
        _languageService.Verify(service => service.GetPackageInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task BulkExportStillDefaultsToCurrentRepository()
    {
        _git.Setup(git => git.DiscoverRepoRootAsync(Environment.CurrentDirectory, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_directory.DirectoryPath);
        _languageService.Setup(service => service.DiscoverPackagesAsync(
            (string)(NormalizedPath)_directory.DirectoryPath, null, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<PackageInfo>());

        var response = await Invoke("--out-dir", "out");

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(response.ToString(), Does.Contain("No packages found"));
        _git.VerifyAll();
    }

    [Test]
    public void CancellationPropagates()
    {
        _languageService.Setup(service => service.GetPackageInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var parseResult = _tool.GetCommandInstances().Single().Parse(["--package-path", _directory.DirectoryPath]);

        Assert.ThrowsAsync<OperationCanceledException>(() => _tool.HandleCommand(parseResult, CancellationToken.None));
    }

    private async Task<CommandResponse> Invoke(params string[] args)
    {
        var root = new RootCommand
        {
            SharedOptions.Format,
            new Command(SharedCommandGroups.EngSys.Verb) { _tool.GetCommandInstances().Single() }
        };
        var parseResult = root.Parse(["eng", "package-info", .. args]);
        Assert.That(parseResult.Errors, Is.Empty);
        return await _tool.HandleCommand(parseResult, CancellationToken.None);
    }
}
