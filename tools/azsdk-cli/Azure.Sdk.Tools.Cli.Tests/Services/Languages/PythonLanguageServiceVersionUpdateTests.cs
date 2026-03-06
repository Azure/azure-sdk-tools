// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
internal class PythonLanguageServiceVersionUpdateTests
{
    private Mock<IProcessHelper> _processHelperMock = null!;
    private Mock<INpxHelper> _npxHelperMock = null!;
    private Mock<IPythonHelper> _pythonHelperMock = null!;
    private Mock<IGitHelper> _gitHelperMock = null!;
    private Mock<ICommonValidationHelpers> _commonValidationHelpersMock = null!;
    private PythonLanguageService _languageService = null!;

    [SetUp]
    public void SetUp()
    {
        _processHelperMock = new Mock<IProcessHelper>();
        _npxHelperMock = new Mock<INpxHelper>();
        _pythonHelperMock = new Mock<IPythonHelper>();
        _gitHelperMock = new Mock<IGitHelper>();
        _gitHelperMock.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-python");
        _commonValidationHelpersMock = new Mock<ICommonValidationHelpers>();

        _languageService = new PythonLanguageService(
            _processHelperMock.Object,
            _pythonHelperMock.Object,
            _npxHelperMock.Object,
            _gitHelperMock.Object,
            NullLogger<PythonLanguageService>.Instance,
            _commonValidationHelpersMock.Object,
            Mock.Of<IPackageInfoHelper>(),
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            Mock.Of<IChangelogHelper>());
    }

    #region UpdatePackageVersionInFilesAsync Tests

    [Test]
    public async Task UpdateVersionInFiles_UpdatesVersionPy_WhenFileExists()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-version-update-test");
        var packageDir = Path.Combine(tempDir.DirectoryPath, "azure", "mypackage");
        Directory.CreateDirectory(packageDir);

        var versionPyPath = Path.Combine(packageDir, "_version.py");
        await File.WriteAllTextAsync(versionPyPath, "VERSION = \"1.0.0\"\n");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(versionPyPath);
        Assert.That(updatedContent, Does.Contain("VERSION = \"2.0.0\""));
    }

    [Test]
    public async Task UpdateVersionInFiles_UpdatesLegacyVersionPy_WhenOnlyVersionPyExists()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-legacy-version-test");
        var packageDir = Path.Combine(tempDir.DirectoryPath, "azure", "mypackage");
        Directory.CreateDirectory(packageDir);

        var versionPyPath = Path.Combine(packageDir, "version.py");
        await File.WriteAllTextAsync(versionPyPath, "VERSION = \"1.0.0b1\"\n");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "1.0.0b2", "beta");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedContent = await File.ReadAllTextAsync(versionPyPath);
        Assert.That(updatedContent, Does.Contain("VERSION = \"1.0.0b2\""));
    }

    [Test]
    public async Task UpdateVersionInFiles_ReturnsFailure_WhenVersionPatternNotFound()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-version-pattern-missing-test");
        var packageDir = Path.Combine(tempDir.DirectoryPath, "azure", "mypackage");
        Directory.CreateDirectory(packageDir);

        var versionPyPath = Path.Combine(packageDir, "_version.py");
        await File.WriteAllTextAsync(versionPyPath, "__version__ = \"1.0.0\"\n");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("VERSION pattern"));
        var unchangedContent = await File.ReadAllTextAsync(versionPyPath);
        Assert.That(unchangedContent, Does.Contain("__version__ = \"1.0.0\""));
    }

    [Test]
    public async Task UpdateVersionInFiles_Succeeds_WhenVersionAlreadySet()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-version-already-set-test");
        var packageDir = Path.Combine(tempDir.DirectoryPath, "azure", "mypackage");
        Directory.CreateDirectory(packageDir);

        var versionPyPath = Path.Combine(packageDir, "_version.py");
        await File.WriteAllTextAsync(versionPyPath, "VERSION = \"2.0.0\"\n");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var content = await File.ReadAllTextAsync(versionPyPath);
        Assert.That(content, Does.Contain("VERSION = \"2.0.0\""));
    }

    [Test]
    public async Task UpdateVersionInFiles_PrefersUnderscoreVersionPy_WhenBothExist()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-prefer-underscore-test");
        var packageDir = Path.Combine(tempDir.DirectoryPath, "azure", "mypackage");
        Directory.CreateDirectory(packageDir);

        await File.WriteAllTextAsync(Path.Combine(packageDir, "_version.py"), "VERSION = \"1.0.0\"\n");
        await File.WriteAllTextAsync(Path.Combine(packageDir, "version.py"), "VERSION = \"1.0.0\"\n");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var underscoreContent = await File.ReadAllTextAsync(Path.Combine(packageDir, "_version.py"));
        var plainContent = await File.ReadAllTextAsync(Path.Combine(packageDir, "version.py"));
        Assert.That(underscoreContent, Does.Contain("VERSION = \"2.0.0\""));
        Assert.That(plainContent, Does.Contain("VERSION = \"1.0.0\"")); // unchanged
    }

    [Test]
    public async Task UpdateVersionInFiles_ReturnsFailure_WhenNoVersionFileFound()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-no-version-file-test");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.ResponseErrors.FirstOrDefault(), Does.Contain("_version.py").Or.Contains("version.py"));
    }

    [Test]
    public async Task UpdateVersionInFiles_UpdatesSetupPyClassifier_StableVersion()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-setup-classifier-test");
        var packageDir = Path.Combine(tempDir.DirectoryPath, "azure", "mypackage");
        Directory.CreateDirectory(packageDir);

        await File.WriteAllTextAsync(Path.Combine(packageDir, "_version.py"), "VERSION = \"1.0.0b1\"\n");

        var setupPyContent = @"from setuptools import setup
setup(
    name=""azure-mypackage"",
    version=""1.0.0b1"",
    classifiers=[
        ""Development Status :: 4 - Beta"",
        ""Programming Language :: Python"",
    ],
)";
        var setupPyPath = Path.Combine(tempDir.DirectoryPath, "setup.py");
        await File.WriteAllTextAsync(setupPyPath, setupPyContent);

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "1.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedSetup = await File.ReadAllTextAsync(setupPyPath);
        Assert.That(updatedSetup, Does.Contain("Development Status :: 5 - Production/Stable"));
        Assert.That(updatedSetup, Does.Not.Contain("Development Status :: 4 - Beta"));
    }

    [Test]
    public async Task UpdateVersionInFiles_UpdatesSetupPyClassifier_BetaVersion()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-beta-classifier-test");
        var packageDir = Path.Combine(tempDir.DirectoryPath, "azure", "mypackage");
        Directory.CreateDirectory(packageDir);

        await File.WriteAllTextAsync(Path.Combine(packageDir, "_version.py"), "VERSION = \"1.0.0\"\n");

        var setupPyContent = @"from setuptools import setup
setup(
    name=""azure-mypackage"",
    classifiers=[
        ""Development Status :: 5 - Production/Stable"",
    ],
)";
        var setupPyPath = Path.Combine(tempDir.DirectoryPath, "setup.py");
        await File.WriteAllTextAsync(setupPyPath, setupPyContent);

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "1.1.0b1", "beta");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedSetup = await File.ReadAllTextAsync(setupPyPath);
        Assert.That(updatedSetup, Does.Contain("Development Status :: 4 - Beta"));
        Assert.That(updatedSetup, Does.Not.Contain("Development Status :: 5 - Production/Stable"));
    }

    [Test]
    public async Task UpdateVersionInFiles_SkipsExcludedDirectories_WhenSearchingForVersionPy()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-skip-dirs-test");

        // Place version.py only in excluded directories - should not be found
        var testsDir = Path.Combine(tempDir.DirectoryPath, "tests");
        Directory.CreateDirectory(testsDir);
        await File.WriteAllTextAsync(Path.Combine(testsDir, "_version.py"), "VERSION = \"1.0.0\"\n");

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "2.0.0", "stable");

        // Assert - should fail since version.py in excluded dir is not found
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        var testsVersionContent = await File.ReadAllTextAsync(Path.Combine(testsDir, "_version.py"));
        Assert.That(testsVersionContent, Does.Contain("VERSION = \"1.0.0\"")); // unchanged
    }

    [Test]
    public async Task UpdateVersionInFiles_UpdatesPyprojectToml_WhenSetupPyNotPresent()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-pyproject-classifier-test");
        var packageDir = Path.Combine(tempDir.DirectoryPath, "azure", "mypackage");
        Directory.CreateDirectory(packageDir);

        await File.WriteAllTextAsync(Path.Combine(packageDir, "_version.py"), "VERSION = \"1.0.0b1\"\n");

        var pyprojectContent = @"[project]
name = ""azure-mypackage""
version = ""1.0.0b1""
classifiers = [
    ""Development Status :: 4 - Beta"",
]";
        var pyprojectPath = Path.Combine(tempDir.DirectoryPath, "pyproject.toml");
        await File.WriteAllTextAsync(pyprojectPath, pyprojectContent);

        // Act
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "1.0.0", "stable");

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedPyproject = await File.ReadAllTextAsync(pyprojectPath);
        Assert.That(updatedPyproject, Does.Contain("Development Status :: 5 - Production/Stable"));
    }

    [Test]
    public async Task UpdateVersionInFiles_DetectsBetaFromVersionString_WhenReleaseTypeIsNull()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-detect-beta-test");
        var packageDir = Path.Combine(tempDir.DirectoryPath, "azure", "mypackage");
        Directory.CreateDirectory(packageDir);

        await File.WriteAllTextAsync(Path.Combine(packageDir, "_version.py"), "VERSION = \"1.0.0\"\n");

        var setupPyContent = @"setup(
    classifiers=[
        ""Development Status :: 5 - Production/Stable"",
    ],
)";
        await File.WriteAllTextAsync(Path.Combine(tempDir.DirectoryPath, "setup.py"), setupPyContent);

        // Act - releaseType is null but version string has 'b' prerelease marker
        var result = await InvokeUpdatePackageVersionInFilesAsync(
            tempDir.DirectoryPath, "1.1.0b2", null);

        // Assert
        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        var updatedSetup = await File.ReadAllTextAsync(Path.Combine(tempDir.DirectoryPath, "setup.py"));
        Assert.That(updatedSetup, Does.Contain("Development Status :: 4 - Beta"));
    }

    #endregion

    /// <summary>
    /// Uses reflection to call the protected UpdatePackageVersionInFilesAsync method for testing.
    /// </summary>
    private async Task<PackageOperationResponse> InvokeUpdatePackageVersionInFilesAsync(
        string packagePath, string version, string? releaseType)
    {
        var method = typeof(PythonLanguageService)
            .GetMethod("UpdatePackageVersionInFilesAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.That(method, Is.Not.Null, "UpdatePackageVersionInFilesAsync method not found");

        var task = (Task<PackageOperationResponse>)method!.Invoke(
            _languageService,
            [packagePath, version, releaseType, CancellationToken.None])!;

        return await task;
    }
}
