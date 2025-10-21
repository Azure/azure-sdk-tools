// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.VerifySetup;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Verify;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Verify;

internal class VerifySetupToolTests
{
    private VerifySetupTool tool;
    private Mock<IProcessHelper> mockProcessHelper;
    private Mock<ILanguageSpecificResolver<IEnvRequirementsCheck>> mockEnvRequirementsCheck;
    private TestLogger<VerifySetupTool> logger;

    [SetUp]
    public void Setup()
    {
        mockProcessHelper = new Mock<IProcessHelper>();
        mockEnvRequirementsCheck = new Mock<ILanguageSpecificResolver<IEnvRequirementsCheck>>();
        logger = new TestLogger<VerifySetupTool>();

        tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            mockEnvRequirementsCheck.Object
        );

        SetupSuccessfulProcessMocks();
    }

    private void SetupSuccessfulProcessMocks()
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

        foreach (var (command, output) in successfulCommands)
        {
            mockProcessHelper
                .Setup(x => x.Run(
                    It.Is<ProcessOptions>(opt => opt.Command.Contains(command) || opt.Args.Contains(command)), // Handle cases where command might be wrapped
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult 
                { 
                    ExitCode = 0, 
                    OutputDetails = new List<(StdioLevel, string)> { (StdioLevel.StandardOutput, output) } 
                });
        }
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

    private void SetupLanguageRequirementsMocks(Dictionary<SdkLanguage, (string requirement, string[] checkCommand, List<string> instructions)> languageSpecs)
    {
        mockEnvRequirementsCheck
            .Setup(x => x.Resolve(It.IsAny<List<SdkLanguage>>(), It.IsAny<CancellationToken>()))
            .Returns((List<SdkLanguage> langs, CancellationToken _) =>
            {
                var checkers = new List<IEnvRequirementsCheck?>();
                foreach (var lang in langs)
                {
                    if (languageSpecs.ContainsKey(lang))
                    {
                        var mockChecker = new Mock<IEnvRequirementsCheck>();
                        var spec = languageSpecs[lang];
                        mockChecker
                            .Setup(x => x.GetRequirements(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new List<SetupRequirements.Requirement>
                            {
                                new SetupRequirements.Requirement
                                {
                                    requirement = spec.requirement,
                                    check = spec.checkCommand,
                                    instructions = spec.instructions
                                }
                            });
                        checkers.Add(mockChecker.Object);
                    }
                    else
                    {
                        checkers.Add(null); // Language not supported
                    }
                }
                return checkers;
            });
    }

    [Test]
    public async Task VerifySetup_Succeeds_WhenAllRequirementsMet()
    {
        // Arrange
        var languageSpecs = new Dictionary<SdkLanguage, (string requirement, string[] checkCommand, List<string> instructions)>
        {
            { SdkLanguage.Python, ("Python >= 3.8", new[] { "python", "--version" }, new List<string> { "Install Python 3.8 or higher" }) }
        };
        SetupLanguageRequirementsMocks(languageSpecs);

        // Act
        var result = await tool.VerifySetup(new List<SdkLanguage> { SdkLanguage.Python }, "/test/path");

        // Assert
        Assert.That(result.AllRequirementsSatisfied, Is.True);
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
        SetupLanguageRequirementsMocks(languageSpecs);

        SetupFailedProcessMock("node", 1, "node: command not found");

        // Act
        var result = await tool.VerifySetup(new List<SdkLanguage> { SdkLanguage.Python }, "/test/path");

        // Assert
        Assert.That(result.AllRequirementsSatisfied, Is.False);
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
        SetupLanguageRequirementsMocks(languageSpecs);

        // Act
        var result = await tool.VerifySetup(new List<SdkLanguage> { SdkLanguage.Python }, "/test/path");

        // Assert
        Assert.That(result.AllRequirementsSatisfied, Is.False);
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
            { SdkLanguage.Python, ("Python >= 3.8", new[] { "python", "--version" }, new List<string> { "Install Python 3.8" }) },
            { SdkLanguage.Java, ("Java >= 17", new[] { "java", "-version" }, new List<string> { "Install Java 17" }) },
            { SdkLanguage.DotNet, (".NET >= 8.0", new[] { "dotnet", "--version" }, new List<string> { "Install .NET 8.0" }) }
        };

        SetupLanguageRequirementsMocks(languageSpecs);
        SetupFailedProcessMock("java", 1, "java: command not found");

        // Act
        var result = await tool.VerifySetup(new List<SdkLanguage> { SdkLanguage.Python }, "/test/path");

        // Assert
        Assert.That(result.AllRequirementsSatisfied, Is.True);
        Assert.That(result.ResponseError, Is.Null);

        // Verify that only Python language resolver was called, not Java or .NET
        mockEnvRequirementsCheck.Verify(
            x => x.Resolve(It.Is<List<SdkLanguage>>(langs => langs.Contains(SdkLanguage.Python) && langs.Count == 1), It.IsAny<CancellationToken>()),
            Times.Once);

        mockEnvRequirementsCheck.Verify(
            x => x.Resolve(It.Is<List<SdkLanguage>>(langs => langs.Contains(SdkLanguage.Java)), It.IsAny<CancellationToken>()),
            Times.Never);

        mockEnvRequirementsCheck.Verify(
            x => x.Resolve(It.Is<List<SdkLanguage>>(langs => langs.Contains(SdkLanguage.DotNet)), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task VerifySetup_ChecksMultipleSpecifiedLanguages()
    {
        // Arrange
        var languageSpecs = new Dictionary<SdkLanguage, (string, string[], List<string>)>
        {
            { SdkLanguage.Python, ("Python >= 3.8", new[] { "python", "--version" }, new List<string> { "Install Python 3.8" }) },
            { SdkLanguage.Java, ("Java >= 17", new[] { "java", "-version" }, new List<string> { "Install Java 17" }) }
        };

        SetupLanguageRequirementsMocks(languageSpecs);

        // Act - Request both Python and Java
        var result = await tool.VerifySetup(new List<SdkLanguage> { SdkLanguage.Python, SdkLanguage.Java }, "/test/path");

        // Assert
        Assert.That(result.AllRequirementsSatisfied, Is.True);
        Assert.That(result.ResponseError, Is.Null);

        // Verify that resolver was called with both languages
        mockEnvRequirementsCheck.Verify(
            x => x.Resolve(It.Is<List<SdkLanguage>>(langs => langs.Contains(SdkLanguage.Python) && langs.Contains(SdkLanguage.Java) && langs.Count == 2), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task VerifySetup_HandlesInvalidLanguageInput()
    {
        // Mock the resolver to return empty list for invalid languages (simulating no valid languages found)
        mockEnvRequirementsCheck
            .Setup(x => x.Resolve(It.IsAny<List<SdkLanguage>>(), It.IsAny<CancellationToken>()))
            .Returns(new List<IEnvRequirementsCheck?>()); // Return empty list, not null

        // Act - Pass invalid language
        var result = await tool.VerifySetup(new List<SdkLanguage> { (SdkLanguage)(-1) }, "/test/path");

        // Assert - Should succeed with just core requirements
        Assert.That(result.AllRequirementsSatisfied, Is.True);
        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Results, Is.Empty);
    }
}
