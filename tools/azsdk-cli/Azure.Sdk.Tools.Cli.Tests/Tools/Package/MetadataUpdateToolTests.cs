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

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class MetadataUpdateToolTests
{
    #region Test Constants

    private const string TestPackagePath = "/test/package/path";
    private const string TestRepoRoot = "/test/repo/root";
    private const string TestConfigValue = "test-config-value";
    private const string TestSuccessMessage = "Package metadata content is updated.";
    private const string TestErrorMessage = "Test error message";

    // Error message patterns
    private const string EmptyPackagePathError = "Package path is required and cannot be empty.";
    private const string PackageNotFoundError = "Package path does not exist: ";
    private const string RepoRootNotFoundError = "Unable to find git repository root from the provided package path.";

    #endregion

    private MetadataUpdateTool _tool;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<ILanguageSpecificResolver<ILanguagePackageUpdate>> _mockPackageUpdateResolver;
    private Mock<ISpecGenSdkConfigHelper> _mockSpecGenSdkConfigHelper;
    private Mock<ILanguagePackageUpdate> _mockLanguagePackageUpdate;
    private Mock<ILanguageSpecificResolver<IPackageInfoHelper>> _mockPackageInfoResolver;
    private TestLogger<MetadataUpdateTool> _logger;
    private TempDirectory _tempDirectory;
    private PackageInfo _testPackageInfo;
    private PackageOperationResponse _successResponse;
    private PackageOperationResponse _failureResponse;

    [SetUp]
    public void Setup()
    {
        // Create mocks
        _mockGitHelper = new Mock<IGitHelper>();
        _mockPackageUpdateResolver = new Mock<ILanguageSpecificResolver<ILanguagePackageUpdate>>();
        _mockSpecGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();
        _mockLanguagePackageUpdate = new Mock<ILanguagePackageUpdate>();
        _mockPackageInfoResolver = new Mock<ILanguageSpecificResolver<IPackageInfoHelper>>();
        _logger = new TestLogger<MetadataUpdateTool>();

        // Setup package info resolver to return test package info
        var mockPackageInfoHelper = new Mock<IPackageInfoHelper>();
        mockPackageInfoHelper.Setup(x => x.ResolvePackageInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testPackageInfo);
        _mockPackageInfoResolver.Setup(x => x.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPackageInfoHelper.Object);

        // Create temp directory for tests
        _tempDirectory = TempDirectory.Create("MetadataUpdateToolTests");

        // Setup test data
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
            SdkType = SdkType.Dataplane
        };

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
        _tool = new MetadataUpdateTool(
            _mockGitHelper.Object,
            _logger,
            _mockPackageUpdateResolver.Object,
            _mockSpecGenSdkConfigHelper.Object,
            _mockPackageInfoResolver.Object
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
        Assert.That(_tool.GetType().Name, Is.EqualTo("MetadataUpdateTool"));
    }

    #endregion

    #region UpdateMetadataAsync Tests

    [Test]
    public async Task UpdateMetadataAsync_WithNullPackagePath_ShouldReturnFailure()
    {
        // Act
        var result = await _tool.UpdateMetadataAsync(null!, CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Is.EqualTo(EmptyPackagePathError));
    }

    [Test]
    public async Task UpdateMetadataAsync_WithEmptyPackagePath_ShouldReturnFailure()
    {
        // Act
        var result = await _tool.UpdateMetadataAsync(string.Empty, CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Is.EqualTo(EmptyPackagePathError));
    }

    [Test]
    public async Task UpdateMetadataAsync_WithNonExistentPackagePath_ShouldReturnFailure()
    {
        // Arrange
        var nonExistentPath = "/non/existent/path";
        var expectedError = $"{PackageNotFoundError}{nonExistentPath}";

        // Act
        var result = await _tool.UpdateMetadataAsync(nonExistentPath, CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Is.EqualTo(expectedError));
    }

    [Test]
    public async Task UpdateMetadataAsync_WithValidPath_WhenRepoRootNotFound_ShouldReturnFailure()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(testPath)).Returns((string?)null!);

        // Act
        var result = await _tool.UpdateMetadataAsync(testPath, CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Is.EqualTo(RepoRootNotFoundError));
    }

    [Test]
    public async Task UpdateMetadataAsync_WithConfigurationFound_ShouldExecuteScript()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(testPath)).Returns(TestRepoRoot);
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateMetadata))
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
        var result = await _tool.UpdateMetadataAsync(testPath, CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
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
    public async Task UpdateMetadataAsync_WithConfigurationFound_WhenProcessOptionsNull_ShouldFallbackToLanguageService()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(testPath)).Returns(TestRepoRoot);
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateMetadata))
            .ReturnsAsync((SpecGenSdkConfigContentType.Command, TestConfigValue));

        _mockSpecGenSdkConfigHelper.Setup(x => x.CreateProcessOptions(
            It.IsAny<SpecGenSdkConfigContentType>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<int>()))
            .Returns((ProcessOptions)null!);

        _mockPackageUpdateResolver.Setup(x => x.Resolve(testPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockLanguagePackageUpdate.Object);

        _mockLanguagePackageUpdate.Setup(x => x.UpdateMetadataAsync(testPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_successResponse);

        // Act
        var result = await _tool.UpdateMetadataAsync(testPath, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(_successResponse));
        _mockPackageUpdateResolver.Verify(x => x.Resolve(testPath, It.IsAny<CancellationToken>()), Times.Once);
        _mockLanguagePackageUpdate.Verify(x => x.UpdateMetadataAsync(testPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateMetadataAsync_WithoutConfiguration_ShouldUseLanguageService()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(testPath)).Returns(TestRepoRoot);
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateMetadata))
            .ReturnsAsync((SpecGenSdkConfigContentType.Unknown, string.Empty));

        _mockPackageUpdateResolver.Setup(x => x.Resolve(testPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockLanguagePackageUpdate.Object);

        _mockLanguagePackageUpdate.Setup(x => x.UpdateMetadataAsync(testPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_successResponse);

        // Act
        var result = await _tool.UpdateMetadataAsync(testPath, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(_successResponse));
        _mockPackageUpdateResolver.Verify(x => x.Resolve(testPath, It.IsAny<CancellationToken>()), Times.Once);
        _mockLanguagePackageUpdate.Verify(x => x.UpdateMetadataAsync(testPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateMetadataAsync_WhenLanguageResolverReturnsNull_ShouldReturnSuccess()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(testPath)).Returns(TestRepoRoot);
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateMetadata))
            .ReturnsAsync((SpecGenSdkConfigContentType.Unknown, string.Empty));

        _mockPackageUpdateResolver.Setup(x => x.Resolve(testPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ILanguagePackageUpdate?)null);

        // Act
        var result = await _tool.UpdateMetadataAsync(testPath, CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Is.EqualTo("No package metadata updates need to be performed."));
        _mockPackageUpdateResolver.Verify(x => x.Resolve(testPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateMetadataAsync_WhenExceptionThrown_ShouldReturnFailure()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        var expectedException = new InvalidOperationException("Test exception");
        var expectedErrorMessage = $"An error occurred: {expectedException.Message}";

        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(testPath)).Throws(expectedException);

        // Act
        var result = await _tool.UpdateMetadataAsync(testPath, CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Is.EqualTo(expectedErrorMessage));
    }

    [Test]
    public async Task UpdateMetadataAsync_WithCancellationToken_ShouldPassTokenToServices()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        var cancellationToken = new CancellationToken();
        
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(testPath)).Returns(TestRepoRoot);
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateMetadata))
            .ReturnsAsync((SpecGenSdkConfigContentType.Unknown, string.Empty));

        _mockPackageUpdateResolver.Setup(x => x.Resolve(testPath, cancellationToken))
            .ReturnsAsync(_mockLanguagePackageUpdate.Object);

        _mockLanguagePackageUpdate.Setup(x => x.UpdateMetadataAsync(testPath, cancellationToken))
            .ReturnsAsync(_successResponse);

        // Act
        var result = await _tool.UpdateMetadataAsync(testPath, cancellationToken);

        // Assert
        Assert.That(result, Is.EqualTo(_successResponse));
        _mockPackageUpdateResolver.Verify(x => x.Resolve(testPath, cancellationToken), Times.Once);
        _mockLanguagePackageUpdate.Verify(x => x.UpdateMetadataAsync(testPath, cancellationToken), Times.Once);
    }

    #endregion

    #region Integration Tests

    [Test]
    public async Task UpdateMetadataAsync_IntegrationTest_WithScriptConfiguration()
    {
        // Arrange
        var testPath = _tempDirectory.DirectoryPath;
        var scriptContent = "echo 'Updating metadata'";
        
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(testPath)).Returns(TestRepoRoot);
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateMetadata))
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
        var result = await _tool.UpdateMetadataAsync(testPath, CancellationToken.None);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        
        // Verify the complete flow
        _mockSpecGenSdkConfigHelper.Verify(x => x.GetConfigurationAsync(TestRepoRoot, SpecGenSdkConfigType.UpdateMetadata), Times.Once);
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

    #endregion
}
