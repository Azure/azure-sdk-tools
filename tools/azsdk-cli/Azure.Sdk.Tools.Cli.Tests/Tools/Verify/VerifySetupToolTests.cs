// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.SetupRequirements;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Verify;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Verify;

/// <summary>
/// Tests for VerifySetupTool. Uses a mocked IVerifySetupService to verify
/// the tool's delegation, error handling, and command parsing.
/// Service-level logic is tested separately in VerifySetupServiceTests.
/// </summary>
internal class VerifySetupToolTests
{
    private Mock<IVerifySetupService> mockService;
    private TestLogger<VerifySetupTool> logger;
    private Mock<IGitHelper> _mockGitHelper;
    private List<LanguageService> languageServices;

    [SetUp]
    public void Setup()
    {
        mockService = new Mock<IVerifySetupService>();
        logger = new TestLogger<VerifySetupTool>();
        _mockGitHelper = new Mock<IGitHelper>();
        languageServices = [];

        // Default: service returns a successful empty response
        mockService
            .Setup(s => s.VerifySetup(
                It.IsAny<HashSet<SdkLanguage>>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifySetupResponse { Results = [] });
    }

    private VerifySetupTool CreateTool() =>
        new(mockService.Object, logger, _mockGitHelper.Object, languageServices);

    [Test]
    public async Task VerifySetup_DelegatesToService()
    {
        var tool = CreateTool();
        var expectedLangs = new HashSet<SdkLanguage> { SdkLanguage.Python };

        await tool.VerifySetup(expectedLangs, "/test/path", requirementsToInstall: null);

        mockService.Verify(s => s.VerifySetup(
            expectedLangs,
            "/test/path",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task VerifySetup_DelegatesToService_WithInstallList()
    {
        var tool = CreateTool();
        var installList = new List<string> { "tsp", "tsp-client" };

        await tool.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.DotNet }, "/test/path/dotnet", requirementsToInstall: installList);

        mockService.Verify(s => s.VerifySetup(
            It.IsAny<HashSet<SdkLanguage>>(),
            "/test/path/dotnet",
            installList,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task VerifySetup_ReturnsServiceResponse()
    {
        var expected = new VerifySetupResponse
        {
            Results = new List<RequirementCheckResult>
            {
                new() { Requirement = "Node.js", RequirementStatusDetails = "missing" }
            }
        };
        mockService
            .Setup(s => s.VerifySetup(It.IsAny<HashSet<SdkLanguage>>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var tool = CreateTool();
        var result = await tool.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path");

        Assert.That(result, Is.SameAs(expected));
    }

    [Test]
    public async Task VerifySetup_WrapsServiceException_InErrorResponse()
    {
        mockService
            .Setup(s => s.VerifySetup(It.IsAny<HashSet<SdkLanguage>>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var tool = CreateTool();
        var result = await tool.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path");

        Assert.That(result.ResponseError, Does.Contain("boom"));
    }

    [Test]
    public void LanguagesParam_RejectsUnknownLanguages()
    {
        var tool = CreateTool();

        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse("--languages unknown --languages julia");

        Assert.Multiple(() =>
        {
            Assert.That(parseResult.Errors.Any(e => e.Message.Contains("Invalid language 'unknown'")), Is.True);
            Assert.That(parseResult.Errors.Any(e => e.Message.Contains("Invalid language 'julia'")), Is.True);
        });
    }

    [Test]
    public void LanguagesParam_AcceptsLanguages()
    {
        var tool = CreateTool();

        var command = tool.GetCommandInstances().First();

        var parseResult = command.Parse("--languages all");
        Assert.That(parseResult.Errors, Is.Empty);

        parseResult = command.Parse("--languages All");
        Assert.That(parseResult.Errors, Is.Empty);

        foreach (var lang in Enum.GetNames<SdkLanguage>().Where(n => n != nameof(SdkLanguage.Unknown)))
        {
            parseResult = command.Parse($"--languages {lang}");
            Assert.That(parseResult.Errors, Is.Empty);

            parseResult = command.Parse($"--languages {lang.ToLower()}");
            Assert.That(parseResult.Errors, Is.Empty);
        }
    }
}
