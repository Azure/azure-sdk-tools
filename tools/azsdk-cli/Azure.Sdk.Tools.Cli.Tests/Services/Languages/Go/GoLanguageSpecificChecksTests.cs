using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages.Go;

[TestFixture]
internal class GoLanguageSpecificChecksTests
{
    private Mock<IGitHelper> _gitHelperMock = null!;
    private Mock<ICommonValidationHelpers> _commonValidationHelpersMock = null!;
    private GoLanguageService _languageService = null!;
    private TempDirectory _tempDir = null!;

    [SetUp]
    public async Task SetUp()
    {
        _tempDir = TempDirectory.Create("go_checks_test");

        var processHelper = new ProcessHelper(NullLogger<ProcessHelper>.Instance, Mock.Of<IRawOutputHelper>());
        var pr = await processHelper.Run(new ProcessOptions("git", "git.exe", ["init", "."], workingDirectory: _tempDir.DirectoryPath), CancellationToken.None);
        Assert.That(pr.ExitCode, Is.EqualTo(0));

        _gitHelperMock = new Mock<IGitHelper>();
        _gitHelperMock
            .Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDir.DirectoryPath);

        _commonValidationHelpersMock = new Mock<ICommonValidationHelpers>();

        _languageService = new GoLanguageService(
            processHelper,
            new PowershellHelper(NullLogger<PowershellHelper>.Instance, Mock.Of<IRawOutputHelper>()),
            _gitHelperMock.Object,
            NullLogger<GoLanguageService>.Instance,
            _commonValidationHelpersMock.Object,
            Mock.Of<IPackageInfoHelper>(),
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            Mock.Of<IChangelogHelper>());
    }

    [TearDown]
    public void TearDown()
    {
        _tempDir.Dispose();
    }

    #region CheckSpelling Tests

    [Test]
    public async Task CheckSpelling_CallsCommonValidationHelpers_WithCorrectSpellingCheckPath()
    {
        // Arrange
        var packagePath = Path.Combine(_tempDir.DirectoryPath, "sdk", "messaging", "azservicebus");
        Directory.CreateDirectory(packagePath);

        _commonValidationHelpersMock
            .Setup(h => h.CheckSpelling(
                It.IsAny<string>(),
                packagePath,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageCheckResponse(0, "No spelling errors found"));

        // Act
        var result = await _languageService.CheckSpelling(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.CheckStatusDetails, Does.Contain("No spelling errors found"));
        });

        var expectedRelativePath = Path.Combine("sdk", "messaging", "azservicebus");
        var expectedSpellingCheckPath = "." + Path.DirectorySeparatorChar + expectedRelativePath + Path.DirectorySeparatorChar + "**";

        _commonValidationHelpersMock.Verify(
            h => h.CheckSpelling(
                expectedSpellingCheckPath,
                packagePath,
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task CheckSpelling_PassesFixCheckErrors_ToCommonValidationHelpers()
    {
        // Arrange
        var packagePath = Path.Combine(_tempDir.DirectoryPath, "sdk", "storage", "azblob");
        Directory.CreateDirectory(packagePath);

        _commonValidationHelpersMock
            .Setup(h => h.CheckSpelling(
                It.IsAny<string>(),
                packagePath,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageCheckResponse(0, "Spelling errors fixed"));

        // Act
        var result = await _languageService.CheckSpelling(packagePath, fixCheckErrors: true, CancellationToken.None);

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0));

        _commonValidationHelpersMock.Verify(
            h => h.CheckSpelling(
                It.IsAny<string>(),
                packagePath,
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task CheckSpelling_ReturnsFailure_WhenSpellingErrorsFound()
    {
        // Arrange
        var packagePath = Path.Combine(_tempDir.DirectoryPath, "sdk", "storage", "azblob");
        Directory.CreateDirectory(packagePath);

        _commonValidationHelpersMock
            .Setup(h => h.CheckSpelling(
                It.IsAny<string>(),
                packagePath,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageCheckResponse(1, "Spelling errors detected", "Spelling check failed."));

        // Act
        var result = await _languageService.CheckSpelling(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Spelling check failed."));
        });
    }

    #endregion
}
