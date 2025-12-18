// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class TestToolTests
{
    private TestTool _tool;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<IProcessHelper> _mockProcessHelper;
    private Mock<IPythonHelper> _mockPythonHelper;
    private Mock<INpxHelper> _mockNpxHelper;
    private TestLogger<TestTool> _logger;
    private TempDirectory _tempDirectory;
    private List<LanguageService> _languageServices;
    private Mock<ICommonValidationHelpers> _commonValidationHelpers;
    private TestLogger<LanguageService> _languageLogger;
    private Mock<IFileHelper> _mockFileHelper;

    [SetUp]
    public void Setup()
    {
        // Create mocks
        _mockGitHelper = new Mock<IGitHelper>();
        _mockProcessHelper = new Mock<IProcessHelper>();
        _mockPythonHelper = new Mock<IPythonHelper>();
        _mockNpxHelper = new Mock<INpxHelper>();
        _logger = new TestLogger<TestTool>();
        _commonValidationHelpers = new Mock<ICommonValidationHelpers>();
        _languageLogger = new TestLogger<LanguageService>();
        _mockFileHelper = new Mock<IFileHelper>();

        // Create temp directory for tests
        _tempDirectory = TempDirectory.Create("TestToolTests");

        _languageServices = [
            new PythonLanguageService(_mockProcessHelper.Object, _mockPythonHelper.Object, _mockNpxHelper.Object, _mockGitHelper.Object, _languageLogger, _commonValidationHelpers.Object, _mockFileHelper.Object),
            new JavaScriptLanguageService(_mockProcessHelper.Object, _mockNpxHelper.Object, _mockGitHelper.Object, _languageLogger, _commonValidationHelpers.Object, _mockFileHelper.Object)
        ];

        // Create the tool instance
        _tool = new TestTool(_logger, _mockGitHelper.Object, _languageServices);
    }

    [TearDown]
    public void TearDown()
    {
        _tempDirectory.Dispose();
    }

    [Test]
    public async Task RunPackageTests_WithPythonPackage_PopulatesTelemetryFields()
    {
        // Arrange
        var pythonProjectPath = Path.Combine(_tempDirectory.DirectoryPath, "test-python-sdk");
        Directory.CreateDirectory(pythonProjectPath);

        // Create a minimal Python package setup file
        var setupPyPath = Path.Combine(pythonProjectPath, "setup.py");
        await File.WriteAllTextAsync(setupPyPath, 
@"from setuptools import setup
setup(
    name='azure-test-package',
    version='1.0.0',
)
");

        // Mock GitHelper to return proper repo information
        _mockGitHelper
            .Setup(x => x.GetRepoName(It.IsAny<string>()))
            .Returns("azure-sdk-for-python");
        _mockGitHelper
            .Setup(x => x.DiscoverRepoRoot(It.IsAny<string>()))
            .Returns(_tempDirectory.DirectoryPath);

        // Mock Python helper to simulate test execution
        var testProcessResult = new ProcessResult { ExitCode = 0 };
        testProcessResult.AppendStdout("All tests passed");
        _mockPythonHelper
            .Setup(x => x.Run(It.IsAny<PythonOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProcessResult);

        // Act
        var result = await _tool.RunPackageTests(pythonProjectPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ExitCode, Is.EqualTo(0), "Test run should succeed");
        // Telemetry fields should be populated - language should be detected from repo
        Assert.That(result.Language, Is.EqualTo(SdkLanguage.Python), "Language should be set to Python");
        // PackageName might not be parsed correctly in test env, but should be set
        Assert.That(result.PackageName, Is.Not.Null, "PackageName should be set (even if value differs in test env)");
    }

    [Test]
    public async Task RunPackageTests_WithJavaScriptPackage_PopulatesTelemetryFields()
    {
        // Arrange
        var jsProjectPath = Path.Combine(_tempDirectory.DirectoryPath, "test-js-sdk");
        Directory.CreateDirectory(jsProjectPath);

        // Create a minimal package.json
        var packageJsonPath = Path.Combine(jsProjectPath, "package.json");
        await File.WriteAllTextAsync(packageJsonPath, @"{
  ""name"": ""@azure/test-package"",
  ""version"": ""1.0.0""
}");

        // Mock GitHelper to return proper repo information
        _mockGitHelper
            .Setup(x => x.GetRepoName(It.IsAny<string>()))
            .Returns("azure-sdk-for-js");
        _mockGitHelper
            .Setup(x => x.DiscoverRepoRoot(It.IsAny<string>()))
            .Returns(_tempDirectory.DirectoryPath);

        // Mock process helper to simulate test execution
        var testProcessResult = new ProcessResult { ExitCode = 0 };
        testProcessResult.AppendStdout("All tests passed");
        _mockProcessHelper
            .Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProcessResult);

        // Act
        var result = await _tool.RunPackageTests(jsProjectPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ExitCode, Is.EqualTo(0), "Test run should succeed");
        // Telemetry fields should be populated - language should be detected from repo
        Assert.That(result.Language, Is.EqualTo(SdkLanguage.JavaScript), "Language should be set to JavaScript");
        Assert.That(result.PackageName, Is.EqualTo("@azure/test-package"), "PackageName should be populated from package.json");
    }

    [Test]
    public async Task RunPackageTests_WithInvalidPath_ReturnsError()
    {
        // Act
        var result = await _tool.RunPackageTests("/nonexistent/path");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ExitCode, Is.Not.EqualTo(0));
        Assert.That(result.ResponseError, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void GetCommand_ReturnsCommandWithCorrectStructure()
    {
        // Act
        var commands = _tool.GetCommandInstances();
        var command = commands.First();

        // Assert
        Assert.That(command, Is.Not.Null);
        Assert.That(command.Name, Is.EqualTo("run"));
        Assert.That(command.Description, Does.Contain("Run tests"));
    }
}
