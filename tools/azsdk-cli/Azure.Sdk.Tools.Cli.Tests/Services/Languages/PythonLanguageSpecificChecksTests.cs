using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
internal class PythonLanguageSpecificChecksTests
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
        _gitHelperMock.Setup(g => g.GetRepoName(It.IsAny<string>())).Returns("azure-sdk-for-python");
        _commonValidationHelpersMock = new Mock<ICommonValidationHelpers>();

        _languageService = new PythonLanguageService(
            _processHelperMock.Object,
            _pythonHelperMock.Object,
            _npxHelperMock.Object,
            _gitHelperMock.Object,
            NullLogger<PythonLanguageService>.Instance,
            _commonValidationHelpersMock.Object,
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>());
    }

    #region HasCustomizations Tests

    [Test]
    public void HasCustomizations_ReturnsTrue_WhenPatchFileExists()
    {
        using var tempDir = TempDirectory.Create("python-customization-test");
        var azureDir = Path.Combine(tempDir.DirectoryPath, "azure", "test");
        Directory.CreateDirectory(azureDir);
        
        // Create a _patch.py file
        File.WriteAllText(Path.Combine(azureDir, "_patch.py"), "# Custom patch code");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasCustomizations_ReturnsTrue_WhenMultiplePatchFilesExist()
    {
        using var tempDir = TempDirectory.Create("python-multi-patch-test");
        var modelsDir = Path.Combine(tempDir.DirectoryPath, "azure", "test", "models");
        var operationsDir = Path.Combine(tempDir.DirectoryPath, "azure", "test", "operations");
        Directory.CreateDirectory(modelsDir);
        Directory.CreateDirectory(operationsDir);
        
        // Create multiple _patch.py files
        File.WriteAllText(Path.Combine(modelsDir, "_patch.py"), "# Models patch");
        File.WriteAllText(Path.Combine(operationsDir, "_patch.py"), "# Operations patch");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasCustomizations_ReturnsFalse_WhenNoPatchFilesExist()
    {
        using var tempDir = TempDirectory.Create("python-no-customization-test");
        var azureDir = Path.Combine(tempDir.DirectoryPath, "azure", "test");
        Directory.CreateDirectory(azureDir);
        
        // Create a regular Python file (not _patch.py)
        File.WriteAllText(Path.Combine(azureDir, "client.py"), "# Client code");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.False);
    }

    #endregion
}
