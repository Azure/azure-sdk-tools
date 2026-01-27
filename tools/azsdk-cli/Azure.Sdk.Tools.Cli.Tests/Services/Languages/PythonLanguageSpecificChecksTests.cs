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
        _gitHelperMock.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-python");
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
    public void HasCustomizations_ReturnsPath_WhenPatchFileHasNonEmptyAllExport()
    {
        using var tempDir = TempDirectory.Create("python-customization-test");
        var azureDir = Path.Combine(tempDir.DirectoryPath, "azure", "test");
        Directory.CreateDirectory(azureDir);
        
        File.WriteAllText(Path.Combine(azureDir, "_patch.py"), 
            "__all__ = [\"CustomClient\"]\n\nclass CustomClient:\n    pass");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(tempDir.DirectoryPath));
    }

    [Test]
    public void HasCustomizations_ReturnsPath_WhenMultilineAllExport()
    {
        using var tempDir = TempDirectory.Create("python-multiline-test");
        var azureDir = Path.Combine(tempDir.DirectoryPath, "azure", "test");
        Directory.CreateDirectory(azureDir);
        
        File.WriteAllText(Path.Combine(azureDir, "_patch.py"), 
            "__all__ = [\n    \"CustomClient\",\n]");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(tempDir.DirectoryPath));
    }

    [Test]
    public void HasCustomizations_ReturnsNull_WhenPatchFileHasEmptyAllExport()
    {
        using var tempDir = TempDirectory.Create("python-empty-patch-test");
        var azureDir = Path.Combine(tempDir.DirectoryPath, "azure", "test");
        Directory.CreateDirectory(azureDir);
        
        File.WriteAllText(Path.Combine(azureDir, "_patch.py"), "__all__: List[str] = []");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void HasCustomizations_ReturnsNull_WhenNoPatchFilesExist()
    {
        using var tempDir = TempDirectory.Create("python-no-patch-test");
        var azureDir = Path.Combine(tempDir.DirectoryPath, "azure", "test");
        Directory.CreateDirectory(azureDir);
        
        File.WriteAllText(Path.Combine(azureDir, "client.py"), "# Client code");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    #endregion
}
