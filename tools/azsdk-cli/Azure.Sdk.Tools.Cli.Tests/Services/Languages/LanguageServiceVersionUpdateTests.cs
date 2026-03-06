// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

/// <summary>
/// Tests for LanguageService.UpdateVersionAsync and related version update logic.
/// These tests verify the base LanguageService behavior when UpdatePackageVersionInFilesAsync
/// is not overridden by a specific language implementation.
/// </summary>
[TestFixture]
public class LanguageServiceVersionUpdateTests
{
    private Mock<IProcessHelper> _mockProcessHelper = null!;
    private Mock<IGitHelper> _mockGitHelper = null!;
    private Mock<ICommonValidationHelpers> _mockCommonValidationHelpers = null!;
    private Mock<IFileHelper> _mockFileHelper = null!;
    private Mock<ISpecGenSdkConfigHelper> _mockSpecGenSdkConfigHelper = null!;
    private Mock<IChangelogHelper> _mockChangelogHelper = null!;
    private TestableLanguageService _languageService = null!;
    private TempDirectory _tempDirectory = null!;

    private const string TestVersion = "1.0.0";
    private const string TestReleaseDate = "2025-12-01";
    private const string TestChangelogPath = "/test/package/CHANGELOG.md";

    [SetUp]
    public void SetUp()
    {
        _mockProcessHelper = new Mock<IProcessHelper>();
        _mockGitHelper = new Mock<IGitHelper>();
        _mockCommonValidationHelpers = new Mock<ICommonValidationHelpers>();
        _mockFileHelper = new Mock<IFileHelper>();
        _mockSpecGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();
        _mockChangelogHelper = new Mock<IChangelogHelper>();
        _tempDirectory = TempDirectory.Create("LanguageServiceVersionUpdateTests");

        _languageService = new TestableLanguageService(
            _mockProcessHelper.Object,
            _mockGitHelper.Object,
            NullLogger<TestableLanguageService>.Instance,
            _mockCommonValidationHelpers.Object,
            Mock.Of<IPackageInfoHelper>(),
            _mockFileHelper.Object,
            _mockSpecGenSdkConfigHelper.Object,
            _mockChangelogHelper.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _tempDirectory.Dispose();
    }

    /// <summary>
    /// Creates a test PackageInfo with all required members initialized.
    /// </summary>
    private static PackageInfo CreateTestPackageInfo(string? packageVersion = "1.0.0") => new PackageInfo
    {
        PackagePath = "/test/package",
        RepoRoot = "/test/repo",
        RelativePath = "test/package",
        PackageName = "test-package",
        ServiceName = "test-service",
        PackageVersion = packageVersion!,
        SamplesDirectory = "/test/samples",
        Language = SdkLanguage.DotNet
    };

    #region UpdateVersionAsync Tests

    [Test]
    public async Task UpdateVersionAsync_WhenVersionNotProvided_AndPackageVersionNotAvailable_ShouldReturnFailure()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;
        _languageService.SetPackageInfoResult(CreateTestPackageInfo(packageVersion: null));

        // Act
        var result = await _languageService.UpdateVersionAsync(packagePath, null, null, TestReleaseDate, CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("Version is required"));
    }

    [Test]
    public async Task UpdateVersionAsync_WhenChangelogNotFound_ShouldReturnFailure()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;
        _languageService.SetPackageInfoResult(CreateTestPackageInfo(packageVersion: TestVersion));
        _mockChangelogHelper.Setup(x => x.GetChangelogPath(packagePath)).Returns((string?)null);

        // Act
        var result = await _languageService.UpdateVersionAsync(packagePath, null, TestVersion, TestReleaseDate, CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("No CHANGELOG.md found"));
    }

    [Test]
    public async Task UpdateVersionAsync_WhenNoEntryForVersion_ShouldReturnFailure()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;
        _languageService.SetPackageInfoResult(CreateTestPackageInfo(packageVersion: TestVersion));
        _mockChangelogHelper.Setup(x => x.GetChangelogPath(packagePath)).Returns(TestChangelogPath);
        _mockChangelogHelper.Setup(x => x.UpdateReleaseDate(TestChangelogPath, TestVersion, TestReleaseDate))
            .Returns(new ChangelogUpdateResult { Success = false, Message = $"No changelog entry found for version {TestVersion}" });

        // Act
        var result = await _languageService.UpdateVersionAsync(packagePath, null, TestVersion, TestReleaseDate, CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain($"No changelog entry found for version {TestVersion}"));
    }

    [Test]
    public async Task UpdateVersionAsync_WhenChangelogUpdateFails_ShouldReturnFailure()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;
        _languageService.SetPackageInfoResult(CreateTestPackageInfo(packageVersion: TestVersion));
        _mockChangelogHelper.Setup(x => x.GetChangelogPath(packagePath)).Returns(TestChangelogPath);
        _mockChangelogHelper.Setup(x => x.UpdateReleaseDate(TestChangelogPath, TestVersion, TestReleaseDate))
            .Returns(new ChangelogUpdateResult { Success = false, Message = "Failed to update changelog" });

        // Act
        var result = await _languageService.UpdateVersionAsync(packagePath, null, TestVersion, TestReleaseDate, CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("Failed to update changelog"));
    }

    [Test]
    public async Task UpdateVersionAsync_WhenUpdatePackageVersionInFilesReturnsPartial_ShouldReturnPartialDirectly()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;
        _languageService.SetPackageInfoResult(CreateTestPackageInfo(packageVersion: TestVersion));
        _mockChangelogHelper.Setup(x => x.GetChangelogPath(packagePath)).Returns(TestChangelogPath);
        _mockChangelogHelper.Setup(x => x.UpdateReleaseDate(TestChangelogPath, TestVersion, TestReleaseDate))
            .Returns(new ChangelogUpdateResult { Success = true, Message = "Updated" });

        // The base LanguageService.UpdatePackageVersionInFilesAsync returns partial success
        // since it's not implemented for specific languages

        // Act
        var result = await _languageService.UpdateVersionAsync(packagePath, "beta", TestVersion, TestReleaseDate, CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Result, Is.EqualTo("partial"));
        Assert.That(result.Message, Does.Contain("Language-specific version file update not implemented"));
        Assert.That(result.NextSteps, Is.Not.Null);
        Assert.That(result.NextSteps![0], Does.Contain("Manually update the package version"));
    }

    [Test]
    public async Task UpdateVersionAsync_WhenUpdatePackageVersionInFilesReturnsFullSuccess_ShouldReturnFullSuccess()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;
        _languageService.SetPackageInfoResult(CreateTestPackageInfo(packageVersion: TestVersion));
        _mockChangelogHelper.Setup(x => x.GetChangelogPath(packagePath)).Returns(TestChangelogPath);
        _mockChangelogHelper.Setup(x => x.UpdateReleaseDate(TestChangelogPath, TestVersion, TestReleaseDate))
            .Returns(new ChangelogUpdateResult { Success = true, Message = "Updated" });

        // Configure the service to return full success from UpdatePackageVersionInFilesAsync
        _languageService.SetVersionUpdateResult(PackageOperationResponse.CreateSuccess("Version files updated successfully"));

        // Act
        var result = await _languageService.UpdateVersionAsync(packagePath, "stable", TestVersion, TestReleaseDate, CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Does.Contain($"Version {TestVersion} updated with release date {TestReleaseDate}"));
    }

    [Test]
    public async Task UpdateVersionAsync_WhenUpdatePackageVersionInFilesFails_ShouldReturnPartialWithChangelogUpdated()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;
        _languageService.SetPackageInfoResult(CreateTestPackageInfo(packageVersion: TestVersion));
        _mockChangelogHelper.Setup(x => x.GetChangelogPath(packagePath)).Returns(TestChangelogPath);
        _mockChangelogHelper.Setup(x => x.UpdateReleaseDate(TestChangelogPath, TestVersion, TestReleaseDate))
            .Returns(new ChangelogUpdateResult { Success = true, Message = "Updated" });

        // Configure the service to return failure from UpdatePackageVersionInFilesAsync
        _languageService.SetVersionUpdateResult(PackageOperationResponse.CreateFailure(
            "Failed to update version files",
            nextSteps: ["Check the project file format"]));

        // Act
        var result = await _languageService.UpdateVersionAsync(packagePath, "beta", TestVersion, TestReleaseDate, CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded)); // Partial success since changelog was updated
        Assert.That(result.Result, Is.EqualTo("partial"));
        Assert.That(result.Message, Does.Contain($"Changelog release date updated to {TestReleaseDate}"));
        Assert.That(result.Message, Does.Contain("version file update requires additional steps"));
    }

    [Test]
    public async Task UpdateVersionAsync_WithProvidedVersion_ShouldUseProvidedVersion()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;
        var providedVersion = "2.0.0";
        _languageService.SetPackageInfoResult(CreateTestPackageInfo(packageVersion: "1.0.0")); // Different from provided
        _mockChangelogHelper.Setup(x => x.GetChangelogPath(packagePath)).Returns(TestChangelogPath);
        _mockChangelogHelper.Setup(x => x.UpdateReleaseDate(TestChangelogPath, providedVersion, TestReleaseDate))
            .Returns(new ChangelogUpdateResult { Success = true, Message = "Updated" });

        // Act
        var result = await _languageService.UpdateVersionAsync(packagePath, "stable", providedVersion, TestReleaseDate, CancellationToken.None);

        // Assert
        _mockChangelogHelper.Verify(x => x.UpdateReleaseDate(TestChangelogPath, providedVersion, TestReleaseDate), Times.Once);
    }

    [Test]
    public async Task UpdateVersionAsync_WithoutProvidedVersion_ShouldUsePackageVersion()
    {
        // Arrange
        var packagePath = _tempDirectory.DirectoryPath;
        var packageVersion = "1.5.0";
        _languageService.SetPackageInfoResult(CreateTestPackageInfo(packageVersion: packageVersion));
        _mockChangelogHelper.Setup(x => x.GetChangelogPath(packagePath)).Returns(TestChangelogPath);
        _mockChangelogHelper.Setup(x => x.UpdateReleaseDate(TestChangelogPath, packageVersion, TestReleaseDate))
            .Returns(new ChangelogUpdateResult { Success = true, Message = "Updated" });

        // Act
        var result = await _languageService.UpdateVersionAsync(packagePath, "beta", null, TestReleaseDate, CancellationToken.None);

        // Assert
        _mockChangelogHelper.Verify(x => x.UpdateReleaseDate(TestChangelogPath, packageVersion, TestReleaseDate), Times.Once);
    }

    #endregion

    /// <summary>
    /// Testable implementation of LanguageService that allows controlling
    /// the return values of GetPackageInfo and UpdatePackageVersionInFilesAsync.
    /// </summary>
    private class TestableLanguageService : LanguageService
    {
        private PackageInfo? _packageInfoResult;
        private PackageOperationResponse? _versionUpdateResult;

        public TestableLanguageService(
            IProcessHelper processHelper,
            IGitHelper gitHelper,
            ILogger<TestableLanguageService> logger,
            ICommonValidationHelpers commonValidationHelpers,
            IPackageInfoHelper packageInfoHelper,
            IFileHelper fileHelper,
            ISpecGenSdkConfigHelper specGenSdkConfigHelper,
            IChangelogHelper changelogHelper)
            : base(processHelper, gitHelper, logger, commonValidationHelpers, packageInfoHelper, fileHelper, specGenSdkConfigHelper, changelogHelper)
        {
        }

        public override SdkLanguage Language => SdkLanguage.DotNet;

        public void SetPackageInfoResult(PackageInfo? packageInfo)
        {
            _packageInfoResult = packageInfo;
        }

        public void SetVersionUpdateResult(PackageOperationResponse? result)
        {
            _versionUpdateResult = result;
        }

        public override Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_packageInfoResult ?? CreateDefaultPackageInfo());
        }

        private static PackageInfo CreateDefaultPackageInfo() => new PackageInfo
        {
            PackagePath = "/test/package",
            RepoRoot = "/test/repo",
            RelativePath = "test/package",
            PackageName = "test-package",
            ServiceName = "test-service",
            PackageVersion = "1.0.0",
            SamplesDirectory = "/test/samples",
            Language = SdkLanguage.DotNet
        };

        protected override Task<PackageOperationResponse> UpdatePackageVersionInFilesAsync(string packagePath, string version, string? releaseType, CancellationToken ct)
        {
            if (_versionUpdateResult != null)
            {
                return Task.FromResult(_versionUpdateResult);
            }

            // Default behavior: return partial success (base class behavior)
            return base.UpdatePackageVersionInFilesAsync(packagePath, version, releaseType, ct);
        }
    }
}
