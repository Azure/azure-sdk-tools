// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.SetupRequirements;
using Azure.Sdk.Tools.Cli.Tools.Verify;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Verify;

/// <summary>
/// Tests for VerifySetupInstallTool. Exercises the CLI-only install orchestration:
/// --tools flag, --yes flag, and delegation to IVerifySetupService.
/// Interactive prompting is not unit-tested since it uses Console directly.
/// </summary>
internal class VerifySetupInstallToolTests
{
    private Mock<IVerifySetupService> mockService;

    [SetUp]
    public void Setup()
    {
        mockService = new Mock<IVerifySetupService>();
    }

    private VerifySetupInstallTool CreateTool() => new(mockService.Object);

    /// <summary>
    /// Helper: set up the service so the first call (check-only) returns installable failures,
    /// and subsequent calls (with requirementsToInstall) succeed.
    /// </summary>
    private void SetupCheckThenInstall(List<RequirementCheckResult> checkResults)
    {
        // First call: check-only (requirementsToInstall is null) → return failures
        mockService
            .Setup(s => s.VerifySetup(
                It.IsAny<HashSet<SdkLanguage>>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifySetupResponse { Results = checkResults });

        // Second call: install (requirementsToInstall is non-null) → return empty success
        mockService
            .Setup(s => s.VerifySetup(
                It.IsAny<HashSet<SdkLanguage>>(),
                It.IsAny<string>(),
                It.Is<List<string>>(l => l != null && l.Count > 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifySetupResponse { Results = [] });
    }

    [Test]
    public async Task HandleCommand_WithToolsFlag_InstallsExactlyThoseTools()
    {
        mockService
            .Setup(s => s.VerifySetup(
                It.IsAny<HashSet<SdkLanguage>>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifySetupResponse { Results = [] });

        var tool = CreateTool();
        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse("--tools tsp tsp-client --package-path /test/path");

        await tool.HandleCommand(parseResult, CancellationToken.None);

        mockService.Verify(s => s.VerifySetup(
            It.IsAny<HashSet<SdkLanguage>>(),
            "/test/path",
            It.Is<List<string>>(l => l.Contains("tsp") && l.Contains("tsp-client")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleCommand_WithYesFlag_InstallsAllInstallable()
    {
        SetupCheckThenInstall([
            new RequirementCheckResult
            {
                Requirement = "tsp",
                IsAutoInstallable = true,
                AutoInstallAttempted = false,
                RequirementStatusDetails = "missing"
            },
            new RequirementCheckResult
            {
                Requirement = "Node.js (>= 22.16.0)",
                IsAutoInstallable = false,
                AutoInstallAttempted = false,
                RequirementStatusDetails = "missing"
            }
        ]);

        var tool = CreateTool();
        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse("--yes --package-path /test/path");

        await tool.HandleCommand(parseResult, CancellationToken.None);

        // Should have called VerifySetup twice: first to check, then to install only the installable one
        mockService.Verify(s => s.VerifySetup(
            It.IsAny<HashSet<SdkLanguage>>(),
            "/test/path",
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        mockService.Verify(s => s.VerifySetup(
            It.IsAny<HashSet<SdkLanguage>>(),
            "/test/path",
            It.Is<List<string>>(l => l != null && l.Contains("tsp") && l.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleCommand_NothingInstallable_ReturnsCheckResult()
    {
        var expected = new VerifySetupResponse
        {
            Results = [
                new RequirementCheckResult
                {
                    Requirement = "Node.js",
                    IsAutoInstallable = false,
                    AutoInstallAttempted = false,
                    RequirementStatusDetails = "missing"
                }
            ]
        };

        mockService
            .Setup(s => s.VerifySetup(
                It.IsAny<HashSet<SdkLanguage>>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var tool = CreateTool();
        var command = tool.GetCommandInstances().First();
        var parseResult = command.Parse("--yes --package-path /test/path");

        var result = await tool.HandleCommand(parseResult, CancellationToken.None);

        // Should only call once (check), not attempt install since nothing is installable
        mockService.Verify(s => s.VerifySetup(
            It.IsAny<HashSet<SdkLanguage>>(),
            It.IsAny<string>(),
            It.IsAny<List<string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

}
