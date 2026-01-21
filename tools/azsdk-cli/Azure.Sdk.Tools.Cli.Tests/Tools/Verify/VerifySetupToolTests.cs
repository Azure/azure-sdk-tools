// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Verify;
using Microsoft.VisualStudio.Services.CircuitBreaker;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Verify;

internal class VerifySetupToolTests
{
    private Mock<IProcessHelper> mockProcessHelper;
    private Mock<IPythonHelper> mockPythonHelper;
    private TestLogger<VerifySetupTool> logger;
    private List<LanguageService> languageServices;
    private Mock<INpxHelper> _mockNpxHelper;
    private Mock<IPowershellHelper> _mockPowerShellHelper;
    private TestLogger<LanguageService> _languageLogger;
    private Mock<IMicroagentHostService> _mockMicrohostAgent;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<ICommonValidationHelpers> _commonValidationHelpers;

    [SetUp]
    public void Setup()
    {
        mockProcessHelper = new Mock<IProcessHelper>();
        mockPythonHelper = new Mock<IPythonHelper>();
        logger = new TestLogger<VerifySetupTool>();

        _languageLogger = new TestLogger<LanguageService>();
        _mockMicrohostAgent = new Mock<IMicroagentHostService>();
        _mockNpxHelper = new Mock<INpxHelper>();
        _mockPowerShellHelper = new Mock<IPowershellHelper>();
        _mockGitHelper = new Mock<IGitHelper>();
        _commonValidationHelpers = new Mock<ICommonValidationHelpers>();

        _mockGitHelper.Setup(x => x.GetRepoName(It.IsAny<string>()))
        .Returns((string path) =>
        {
            if (path.Contains("python", StringComparison.OrdinalIgnoreCase))
            { return "azure-sdk-for-python"; }
            if (path.Contains("dotnet", StringComparison.OrdinalIgnoreCase))
            { return "azure-azure-sdk-for-net"; }
            if (path.Contains("java", StringComparison.OrdinalIgnoreCase))
            { return "azure-sdk-for-java"; }
            if (path.Contains("js", StringComparison.OrdinalIgnoreCase))
            { return "azure-sdk-for-js"; }
            if (path.Contains("go", StringComparison.OrdinalIgnoreCase))
            { return "azure-sdk-for-go"; }

            return "unknown-repo"; // default fallback
        });

        // Create temp directory for tests

        languageServices = [
            new PythonLanguageService(mockProcessHelper.Object, mockPythonHelper.Object, _mockNpxHelper.Object, _mockGitHelper.Object, _languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>()),
            new JavaLanguageService(mockProcessHelper.Object, _mockGitHelper.Object, new Mock<IMavenHelper>().Object, _mockMicrohostAgent.Object, _languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>()),
            new JavaScriptLanguageService(mockProcessHelper.Object, _mockNpxHelper.Object, _mockGitHelper.Object, _languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>()),
            new GoLanguageService(mockProcessHelper.Object, _mockPowerShellHelper.Object, _mockGitHelper.Object, _languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>()),
            new DotnetLanguageService(mockProcessHelper.Object, _mockPowerShellHelper.Object, _mockGitHelper.Object, _languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>())
        ];

        SetupSuccessfulProcessMocks();
    }

    private void SetupSuccessfulProcessMocks()
    {
        mockProcessHelper
            .Setup(x => x.Run(
                    It.IsAny<ProcessOptions>(), // Handle cases where command might be wrapped
                    It.IsAny<CancellationToken>()))

            .ReturnsAsync((ProcessOptions processOptions, CancellationToken ct) =>
            {
                var successfulCommands = new Dictionary<string, string>
                {
                    { "node", "v22.16.0" },
                    { "npm", "10.5.0" },
                    { "tsp-client", "0.24.1" },
                    { "tsp", "1.0.1" },
                    { "pwsh", "PowerShell 7.2.0" },
                    { "gh", "gh version 2.30.0" },
                    { "python", "Python 3.9.0" },
                    { "java", "java 17.0.1" }
                };
                foreach (var kvp in successfulCommands)
                {
                    var command = kvp.Key;
                    var output = kvp.Value;
                    if (processOptions.Command.Contains(command) || processOptions.Args.Contains(command))
                    {
                        return new ProcessResult
                        {
                            ExitCode = 0,
                            OutputDetails = new List<(StdioLevel, string)> { (StdioLevel.StandardOutput, output) }
                        };
                    }
                }
                return new ProcessResult
                {
                    ExitCode = 1,
                    OutputDetails = new List<(StdioLevel, string)> { (StdioLevel.StandardOutput, "Command not found") }
                };
            });
    }

    private void SetupFailedProcessMock(string command, int exitCode = 1, string errorOutput = "Command not found")
    {
        mockProcessHelper
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(opt => opt.Command.Contains(command) || opt.Args.Contains(command)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = exitCode,
                OutputDetails = new List<(StdioLevel, string)> { (StdioLevel.StandardError, errorOutput) }
            });
    }

    private List<LanguageService> SetupLanguageRequirementsMocks(Dictionary<SdkLanguage, (string requirement, string[] checkCommand, List<string> instructions)> languageSpecs)
    {
        List<LanguageService> langs = [];
        foreach (var kvp in languageSpecs)
        {
            var mockChecker = new Mock<LanguageService>();
            var spec = kvp.Value;
            mockChecker
                    .Setup(x => x.GetRequirements(It.IsAny<string>(), It.IsAny<Dictionary<string, List<SetupRequirements.Requirement>>>(), It.IsAny<CancellationToken>()))
                    .Returns(new List<SetupRequirements.Requirement>
                    {
                            new SetupRequirements.Requirement
                            {
                                requirement = spec.requirement,
                                check = spec.checkCommand,
                                instructions = spec.instructions
                            }
                    });
            mockChecker.Setup(x => x.Language).Returns(kvp.Key);
            langs.Add(mockChecker.Object);
        }
        return langs;
    }

    [Test]
    public async Task VerifySetup_Succeeds_WhenAllRequirementsMet()
    {
        // Arrange
        var languageSpecs = new Dictionary<SdkLanguage, (string requirement, string[] checkCommand, List<string> instructions)>
        {
            { SdkLanguage.Python, ("Python >= 3.8", new[] { "python", "--version" }, new List<string> { "Install Python 3.8 or higher" }) }
        };
        var langTestServices = SetupLanguageRequirementsMocks(languageSpecs);

        // Act
        var tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            _mockGitHelper.Object,
            langTestServices
        );
        var result = await tool.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path/python");

        // Assert
        Assert.That(result.Results, Is.Empty);
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task VerifySetup_Fails_WhenSomeRequirementsNotMet()
    {
        var languageSpecs = new Dictionary<SdkLanguage, (string, string[], List<string>)>
        {
            { SdkLanguage.Python, ("Python >= 3.8", new[] { "python", "--version" }, new List<string> { "Install Python 3.8 or higher" }) }
        };
        var langTestServices = SetupLanguageRequirementsMocks(languageSpecs);

        SetupFailedProcessMock("node", 1, "node: command not found");

        // Act
        var tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            _mockGitHelper.Object,
            langTestServices
        );
        var result = await tool.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path/python");

        // Assert
        Assert.That(result.Results, Is.Not.Empty);
        Assert.That(result.Results.Any(r => r.Requirement.Contains("Node.js")), Is.True);
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task VerifySetup_Fails_WhenSomeRequirementsVersionNotMet()
    {
        var languageSpecs = new Dictionary<SdkLanguage, (string requirement, string[] checkCommand, List<string> instructions)>
        {
            { SdkLanguage.Python, ("Python >= 3.14", new[] { "python", "--version" }, new List<string> { "Install Python 3.14 or higher" }) }
        };
        var languageTestServices = SetupLanguageRequirementsMocks(languageSpecs);

        // Act
        var tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            _mockGitHelper.Object,
            languageTestServices
        );
        var result = await tool.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path/python");

        // Assert
        Assert.That(result.Results, Is.Not.Empty);
        Assert.That(result.Results.Any(r => r.Requirement.Contains("Python")), Is.True);
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task VerifySetup_OnlyChecksSpecifiedLanguages()
    {
        // Arrange - Set up multiple language specs, but only request python
        var languageSpecs = new Dictionary<SdkLanguage, (string, string[], List<string>)>
        {
            { SdkLanguage.Python, ("Python >= 3.14", new[] { "python", "--version" }, new List<string> { "Install Python 3.14" }) },
            { SdkLanguage.Java, ("Java >= 17", new[] { "java", "-version" }, new List<string> { "Install Java 17" }) },
            { SdkLanguage.DotNet, (".NET >= 8.0", new[] { "dotnet", "--version" }, new List<string> { "Install .NET 8.0" }) }
        };

        var languageTestServices = SetupLanguageRequirementsMocks(languageSpecs);
        SetupFailedProcessMock("java", 1, "java: command not found");

        // Act
        var tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            _mockGitHelper.Object,
            languageTestServices
        );
        var result = await tool.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path/python");

        // Assert
        Assert.That(result.Results, Is.Not.Empty);
        Assert.That(result.Results.Count, Is.EqualTo(1));
        Assert.That(result.Results[0].Requirement, Does.Contain("Python"));
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task VerifySetup_ChecksMultipleSpecifiedLanguages()
    {
        // Arrange
        var languageSpecs = new Dictionary<SdkLanguage, (string, string[], List<string>)>
        {
            { SdkLanguage.Python, ("Python >= 3.8", new[] { "python", "--version" }, new List<string> { "Install Python 3.8" }) },
            { SdkLanguage.Java, ("Java >= 17.0", new[] { "java", "-version" }, new List<string> { "Install Java 17" }) }
        };

        var languageTestServices = SetupLanguageRequirementsMocks(languageSpecs);

        // Act - Request both Python and Java
        var tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            _mockGitHelper.Object,
            languageTestServices
        );
        var result = await tool.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python, SdkLanguage.Java }, "/test/path/java");

        // Assert
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task VerifySetup_HandlesInvalidLanguageInput()
    {
        // Act - Pass invalid language
        var tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            _mockGitHelper.Object,
            languageServices
        );
        var result = await tool.VerifySetup(new HashSet<SdkLanguage> { (SdkLanguage)(-1) }, "/test/path");

        // Assert - Should succeed with just core requirements
        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Results, Is.Empty);
    }
}
