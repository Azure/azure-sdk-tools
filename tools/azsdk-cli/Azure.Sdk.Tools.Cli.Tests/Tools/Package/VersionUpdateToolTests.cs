// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Moq;
using System.CommandLine;
using System.CommandLine.Parsing;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Extensions;
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class VersionUpdateToolTests
{
    #region Test Constants

    private const string TestPackagePath = "/test/package/path";
    private const string TestRepoRoot = "/test/repo/root";
    private const string TestConfigValue = "test-config-value";
    private const string TestSuccessMessage = "Version is updated.";
    private const string TestErrorMessage = "Test error message";

    // Error message patterns
    private const string EmptyPackagePathError = "Package path is required and cannot be empty.";
    private const string PackageNotFoundError = "Package path does not exist: ";
    private const string RepoRootNotFoundError = "Unable to find git repository root from the provided package path.";

    #endregion

    private VersionUpdateTool _tool;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<ISpecGenSdkConfigHelper> _mockSpecGenSdkConfigHelper;
    private Mock<LanguageService> _mockLanguageService;
    private TestLogger<VersionUpdateTool> _logger;
    private TempDirectory _tempDirectory;
    private PackageInfo _testPackageInfo;
    private List<LanguageService> _languageServices;
    private PackageOperationResponse _successResponse;
    private PackageOperationResponse _failureResponse;

    [SetUp]
    public void Setup()
    {
        // Create mocks
        _mockGitHelper = new Mock<IGitHelper>();
        _mockSpecGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();
        _logger = new TestLogger<VersionUpdateTool>();

        _mockLanguageService = new Mock<LanguageService>();

        // Create temp directory for tests
        _tempDirectory = TempDirectory.Create("VersionUpdateToolTests");

        // Setup test data FIRST before setting up mocks that use it
        _testPackageInfo = new PackageInfo
        {
            PackagePath = TestPackagePath,
            RepoRoot = TestRepoRoot,
            RelativePath = "test/relative/path",
            PackageName = "test-package",
            ServiceName = "test-service",
            PackageVersion = "1.0.0",
            SamplesDirectory = "/test/samples",
            Language = SdkLanguage.DotNet,
            SdkType = SdkType.Management  // Changed to Management for script execution tests
        };

        // Setup language service to return test package info
        _mockLanguageService.Setup(x => x.GetPackageInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testPackageInfo);
        _mockLanguageService.Setup(x => x.Language).Returns(SdkLanguage.DotNet);
        _languageServices = new List<LanguageService> { _mockLanguageService.Object };

        _successResponse = new PackageOperationResponse
        {
            Result = "succeeded",
            Message = TestSuccessMessage,
            PackageName = _testPackageInfo.PackageName,
            Language = _testPackageInfo.Language,
            PackageType = _testPackageInfo.SdkType
        };

        _failureResponse = new PackageOperationResponse
        {
            ResponseErrors = [TestErrorMessage],
            Result = "failed",
            PackageName = string.Empty,
            Language = SdkLanguage.Unknown,
            PackageType = SdkType.Unknown
        };

        // Create the tool instance
        _tool = new VersionUpdateTool(
            _mockGitHelper.Object,
            _logger,
            _languageServices,
            _mockSpecGenSdkConfigHelper.Object
        );
    }

    [TearDown]
    public void TearDown()
    {
        _tempDirectory.Dispose();
    }

    #region Constructor Tests

    [Test]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act & Assert
        Assert.That(_tool, Is.Not.Null);
        Assert.That(_tool.GetType().Name, Is.EqualTo("VersionUpdateTool"));
    }

    #endregion

    #region UpdateVersionAsync Tests

    [Test]
    public async Task UpdateVersionAsync_WithNullPackagePath_ShouldReturnFailure()
    {
        // Act
        var result = await _tool.UpdateVersionAsync(null!, null, null, null, CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Is.EqualTo(EmptyPackagePathError));
    }

    [Test]
    public async Task UpdateVersionAsync_WithEmptyPackagePath_ShouldReturnFailure()
    {
        // Act
        var result = await _tool.UpdateVersionAsync(string.Empty, null, null, null, CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Is.EqualTo(EmptyPackagePathError));
        Assert.That(result.NextSteps, Is.Not.Null);
        Assert.That(result.NextSteps![0], Does.Contain("Check the running logs"));
    }

    [Test]
    public async Task UpdateVersionAsync_WithNonExistentPackagePath_ShouldReturnFailure()
    {
        // Arrange
        var nonExistentPath = "/non/existent/path";
        var expectedError = $"{PackageNotFoundError}{nonExistentPath}";

        // Act
        var result = await _tool.UpdateVersionAsync(nonExistentPath, null, null, null, CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("does not exist"));
        Assert.That(result.NextSteps, Is.Not.Null);
        Assert.That(result.NextSteps![0], Does.Contain("Check the running logs"));
    }

    [Test]
    public async Task UpdateVersionAsync_WithValidPath_WhenRepoRootNotFound_ShouldReturnFailure()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync((string?)null!);

        // Act
        var result = await _tool.UpdateVersionAsync(testPath, null, null, null, CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Is.EqualTo(RepoRootNotFoundError));
        Assert.That(result.NextSteps, Is.Not.Null);
        Assert.That(result.NextSteps![0], Does.Contain("Check the running logs"));
    }

    [Test]
    public async Task UpdateVersionAsync_WithConfigurationFound_ShouldExecuteScript()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync(TestRepoRoot);
        _mockGitHelper.Setup(x => x.GetRepoNameAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateVersion))
            .ReturnsAsync((SpecGenSdkConfigContentType.Command, TestConfigValue));

        var mockProcessOptions = new ProcessOptions("echo", ["test"]);
        _mockSpecGenSdkConfigHelper.Setup(x => x.CreateProcessOptions(
            SpecGenSdkConfigContentType.Command,
            TestConfigValue,
            TestRepoRoot,
            testPath,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<int>()))
            .Returns(mockProcessOptions);

        _mockSpecGenSdkConfigHelper.Setup(x => x.ExecuteProcessAsync(
            It.IsAny<ProcessOptions>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<PackageInfo>(),
            It.IsAny<string>(),
            It.IsAny<string[]>()))
            .ReturnsAsync(PackageOperationResponse.CreateSuccess("Success", null));

        // Act
        var result = await _tool.UpdateVersionAsync(testPath, "stable", "1.0.0", "2025-11-11", CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.NextSteps, Is.Not.Null);
        Assert.That(result.NextSteps, Has.Count.EqualTo(2));
        Assert.That(result.NextSteps![0], Does.Contain("Review the updated version"));
        Assert.That(result.NextSteps![1], Does.Contain("Run validation checks"));
        _mockSpecGenSdkConfigHelper.Verify(x => x.CreateProcessOptions(
            SpecGenSdkConfigContentType.Command,
            TestConfigValue,
            TestRepoRoot,
            testPath,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<int>()), Times.Once);
        _mockSpecGenSdkConfigHelper.Verify(x => x.ExecuteProcessAsync(
            It.IsAny<ProcessOptions>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<PackageInfo>(),
            It.IsAny<string>(),
            It.IsAny<string[]>()), Times.Once);
    }

    [Test]
    public async Task UpdateVersionAsync_WithConfigurationFound_WhenProcessOptionsNull_ShouldFallbackToLanguageService()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync(TestRepoRoot);
        _mockGitHelper.Setup(x => x.GetRepoNameAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateVersion))
            .ReturnsAsync((SpecGenSdkConfigContentType.Command, TestConfigValue));

        _mockSpecGenSdkConfigHelper.Setup(x => x.CreateProcessOptions(
            It.IsAny<SpecGenSdkConfigContentType>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<int>()))
            .Returns((ProcessOptions)null!);

        _mockLanguageService.Setup(x => x.UpdateVersionAsync(testPath, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_successResponse);

        // Act
        var result = await _tool.UpdateVersionAsync(testPath, null, null, null, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(_successResponse));
        _mockLanguageService.Verify(x => x.UpdateVersionAsync(testPath, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateVersionAsync_WithoutConfiguration_ShouldUseLanguageService()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync(TestRepoRoot);
        _mockGitHelper.Setup(x => x.GetRepoNameAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateVersion))
            .ReturnsAsync((SpecGenSdkConfigContentType.Unknown, string.Empty));

        _mockLanguageService.Setup(x => x.UpdateVersionAsync(testPath, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_successResponse);

        // Act
        var result = await _tool.UpdateVersionAsync(testPath, null, null, null, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(_successResponse));
        _mockLanguageService.Verify(x => x.UpdateVersionAsync(testPath, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateVersionAsync_WhenExceptionThrown_ShouldReturnFailure()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        var expectedException = new InvalidOperationException("Test exception");
        var expectedErrorMessage = $"An error occurred: {expectedException.Message}";

        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(testPath, It.IsAny<CancellationToken>())).ThrowsAsync(expectedException);

        // Act
        var result = await _tool.UpdateVersionAsync(testPath, null, null, null, CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Is.EqualTo(expectedErrorMessage));
        Assert.That(result.NextSteps, Is.Not.Null);
        Assert.That(result.NextSteps, Has.Count.EqualTo(4));
        Assert.That(result.NextSteps![0], Does.Contain("Check the running logs"));
        Assert.That(result.NextSteps![1], Does.Contain("Resolve the issue"));
        Assert.That(result.NextSteps![2], Does.Contain("Re-run the tool"));
        Assert.That(result.NextSteps![3], Does.Contain("Run verify setup tool"));
    }

    [Test]
    public async Task UpdateVersionAsync_WithCancellationToken_ShouldPassTokenToServices()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        var cancellationToken = new CancellationToken();
        
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync(TestRepoRoot);
        _mockGitHelper.Setup(x => x.GetRepoNameAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateVersion))
            .ReturnsAsync((SpecGenSdkConfigContentType.Unknown, string.Empty));

        _mockLanguageService.Setup(x => x.UpdateVersionAsync(testPath, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), cancellationToken))
            .ReturnsAsync(_successResponse);

        // Act
        var result = await _tool.UpdateVersionAsync(testPath, null, null, null, cancellationToken);

        // Assert
        Assert.That(result, Is.EqualTo(_successResponse));
        _mockLanguageService.Verify(x => x.UpdateVersionAsync(testPath, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), cancellationToken), Times.Once);
    }

    #endregion

    #region Integration Tests

    [Test]
    public async Task UpdateVersionAsync_IntegrationTest_WithScriptConfiguration()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        var scriptContent = "echo 'Updating version'";
        
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync(TestRepoRoot);
        _mockGitHelper.Setup(x => x.GetRepoNameAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateVersion))
            .ReturnsAsync((SpecGenSdkConfigContentType.Command, scriptContent));

        var processOptions = new ProcessOptions("echo", ["test"]);
        _mockSpecGenSdkConfigHelper.Setup(x => x.CreateProcessOptions(
            SpecGenSdkConfigContentType.Command,
            scriptContent,
            TestRepoRoot,
            testPath,
            It.Is<Dictionary<string, string>>(d => 
                d["SdkRepoPath"] == TestRepoRoot && 
                d["PackagePath"] == testPath),
            It.IsAny<int>()))
            .Returns(processOptions);

        _mockSpecGenSdkConfigHelper.Setup(x => x.ExecuteProcessAsync(
            It.IsAny<ProcessOptions>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<PackageInfo>(),
            It.IsAny<string>(),
            It.IsAny<string[]>()))
            .ReturnsAsync(PackageOperationResponse.CreateSuccess("Success", null));

        // Act
        var result = await _tool.UpdateVersionAsync(testPath, "stable", "1.0.0", "2025-11-11", CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        
        // Verify the complete flow
        _mockSpecGenSdkConfigHelper.Verify(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateVersion), Times.Once);
        _mockSpecGenSdkConfigHelper.Verify(x => x.CreateProcessOptions(
            SpecGenSdkConfigContentType.Command,
            scriptContent,
            TestRepoRoot,
            testPath,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<int>()), Times.Once);
        _mockSpecGenSdkConfigHelper.Verify(x => x.ExecuteProcessAsync(
            It.IsAny<ProcessOptions>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<PackageInfo>(),
            It.IsAny<string>(),
            It.IsAny<string[]>()), Times.Once);
    }

    [Test]
    public async Task UpdateVersionAsync_WithAllOptionalParameters_ShouldPassAllToScript()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        var releaseType = "beta";
        var version = "3.0.0-beta.1";
        var releaseDate = "2025-11-15";
        
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync(TestRepoRoot);
        _mockGitHelper.Setup(x => x.GetRepoNameAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateVersion))
            .ReturnsAsync((SpecGenSdkConfigContentType.Command, "test-command"));

        var processOptions = new ProcessOptions("echo", ["test"]);
        _mockSpecGenSdkConfigHelper.Setup(x => x.CreateProcessOptions(
            It.IsAny<SpecGenSdkConfigContentType>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<Dictionary<string, string>>(d => 
                d.GetValueOrDefault("ReleaseType") == releaseType &&
                d.GetValueOrDefault("Version") == version &&
                d.GetValueOrDefault("ReleaseDate") == releaseDate),
            It.IsAny<int>()))
            .Returns(processOptions);

        _mockSpecGenSdkConfigHelper.Setup(x => x.ExecuteProcessAsync(
            It.IsAny<ProcessOptions>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<PackageInfo>(),
            It.IsAny<string>(),
            It.IsAny<string[]>()))
            .ReturnsAsync(PackageOperationResponse.CreateSuccess("Success", null));

        // Act
        var result = await _tool.UpdateVersionAsync(testPath, releaseType, version, releaseDate, CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        _mockSpecGenSdkConfigHelper.Verify(x => x.CreateProcessOptions(
            It.IsAny<SpecGenSdkConfigContentType>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<Dictionary<string, string>>(d => 
                d.GetValueOrDefault("ReleaseType") == releaseType &&
                d.GetValueOrDefault("Version") == version &&
                d.GetValueOrDefault("ReleaseDate") == releaseDate),
            It.IsAny<int>()), Times.Once);
    }

    [Test]
    public async Task UpdateVersionAsync_WithNoOptionalParameters_ShouldDefaultToBeta()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync(TestRepoRoot);
        _mockGitHelper.Setup(x => x.GetRepoNameAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateVersion))
            .ReturnsAsync((SpecGenSdkConfigContentType.Command, "test-command"));

        var processOptions = new ProcessOptions("echo", ["test"]);
        _mockSpecGenSdkConfigHelper.Setup(x => x.CreateProcessOptions(
            It.IsAny<SpecGenSdkConfigContentType>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<Dictionary<string, string>>(d => d.GetValueOrDefault("ReleaseType") == "beta"),
            It.IsAny<int>()))
            .Returns(processOptions);

        _mockSpecGenSdkConfigHelper.Setup(x => x.ExecuteProcessAsync(
            It.IsAny<ProcessOptions>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<PackageInfo>(),
            It.IsAny<string>(),
            It.IsAny<string[]>()))
            .ReturnsAsync(PackageOperationResponse.CreateSuccess("Success", null));

        // Act
        var result = await _tool.UpdateVersionAsync(testPath, null, null, null, CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        _mockSpecGenSdkConfigHelper.Verify(x => x.CreateProcessOptions(
            It.IsAny<SpecGenSdkConfigContentType>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<Dictionary<string, string>>(d => d.GetValueOrDefault("ReleaseType") == "beta"),
            It.IsAny<int>()), Times.Once);
    }

    [Test]
    public async Task UpdateVersionAsync_WithOptionalParameters_ShouldPassToLanguageService()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        var version = "4.0.0";
        var releaseDate = "2025-12-01";
        
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync(TestRepoRoot);
        _mockGitHelper.Setup(x => x.GetRepoNameAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateVersion))
            .ReturnsAsync((SpecGenSdkConfigContentType.Unknown, string.Empty));

        _mockLanguageService.Setup(x => x.UpdateVersionAsync(testPath, It.IsAny<string?>(), version, releaseDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_successResponse);

        // Act
        var result = await _tool.UpdateVersionAsync(testPath, null, version, releaseDate, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(_successResponse));
        _mockLanguageService.Verify(x => x.UpdateVersionAsync(testPath, It.IsAny<string?>(), version, releaseDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateVersionAsync_WithInvalidReleaseDateFormat_ShouldReturnFailure()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        var invalidReleaseDate = "11-12-2025"; // Wrong format (MM-DD-YYYY)

        // Act
        var result = await _tool.UpdateVersionAsync(testPath, null, null, invalidReleaseDate, CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("Invalid release date format"));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("YYYY-MM-DD"));
        Assert.That(result.NextSteps, Is.Not.Null);
        Assert.That(result.NextSteps, Has.Count.EqualTo(2));
        Assert.That(result.NextSteps![0], Does.Contain("Provide the release date in the correct format"));
        Assert.That(result.NextSteps![1], Does.Contain("Re-run the tool"));
    }

    [Test]
    public async Task UpdateVersionAsync_WithValidReleaseDateFormat_ShouldSucceed()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        var validReleaseDate = "2025-12-25"; // Correct format (YYYY-MM-DD)
        
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync(TestRepoRoot);
        _mockGitHelper.Setup(x => x.GetRepoNameAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateVersion))
            .ReturnsAsync((SpecGenSdkConfigContentType.Command, "test-command"));

        var processOptions = new ProcessOptions("echo", ["test"]);
        _mockSpecGenSdkConfigHelper.Setup(x => x.CreateProcessOptions(
            It.IsAny<SpecGenSdkConfigContentType>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<Dictionary<string, string>>(d => d.ContainsKey("ReleaseDate") && d["ReleaseDate"] == validReleaseDate),
            It.IsAny<int>()))
            .Returns(processOptions);

        _mockSpecGenSdkConfigHelper.Setup(x => x.ExecuteProcessAsync(
            It.IsAny<ProcessOptions>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<PackageInfo>(),
            It.IsAny<string>(),
            It.IsAny<string[]>()))
            .ReturnsAsync(PackageOperationResponse.CreateSuccess("Success", null));

        // Act
        var result = await _tool.UpdateVersionAsync(testPath, "beta", null, validReleaseDate, CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
    }

    [Test]
    public async Task UpdateVersionAsync_WithEmptyReleaseDate_ShouldSetToCurrentDate()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        var expectedDatePattern = DateTime.Now.ToString("yyyy-MM-dd");
        
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync(TestRepoRoot);
        _mockGitHelper.Setup(x => x.GetRepoNameAsync(testPath, It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateVersion))
            .ReturnsAsync((SpecGenSdkConfigContentType.Command, "test-command"));

        var processOptions = new ProcessOptions("echo", ["test"]);
        _mockSpecGenSdkConfigHelper.Setup(x => x.CreateProcessOptions(
            It.IsAny<SpecGenSdkConfigContentType>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<Dictionary<string, string>>(d => d.ContainsKey("ReleaseDate") && d["ReleaseDate"] == expectedDatePattern),
            It.IsAny<int>()))
            .Returns(processOptions);

        _mockSpecGenSdkConfigHelper.Setup(x => x.ExecuteProcessAsync(
            It.IsAny<ProcessOptions>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<PackageInfo>(),
            It.IsAny<string>(),
            It.IsAny<string[]>()))
            .ReturnsAsync(PackageOperationResponse.CreateSuccess("Success", null));

        // Act
        var result = await _tool.UpdateVersionAsync(testPath, "beta", null, null, CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        _mockSpecGenSdkConfigHelper.Verify(x => x.CreateProcessOptions(
            It.IsAny<SpecGenSdkConfigContentType>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<Dictionary<string, string>>(d => d.ContainsKey("ReleaseDate") && d["ReleaseDate"] == expectedDatePattern),
            It.IsAny<int>()), Times.Once);
    }

    #endregion
}
