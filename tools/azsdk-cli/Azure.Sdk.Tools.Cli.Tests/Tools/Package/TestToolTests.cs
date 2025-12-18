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
