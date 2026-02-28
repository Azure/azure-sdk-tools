// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.SetupRequirements;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.SetupRequirements;

/// <summary>
/// Tests for VerifySetupService. These exercise the core verification and installation logic
/// against mocked process/git dependencies.
/// </summary>
internal class VerifySetupServiceTests
{
    private Mock<IProcessHelper> mockProcessHelper;
    private TestLogger<VerifySetupService> serviceLogger;
    private VerifySetupService verifySetupService;
    private Mock<IGitHelper> _mockGitHelper;

    [SetUp]
    public void Setup()
    {
        mockProcessHelper = new Mock<IProcessHelper>();
        serviceLogger = new TestLogger<VerifySetupService>();

        _mockGitHelper = new Mock<IGitHelper>();

        _mockGitHelper.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((string path, CancellationToken _) =>
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

        // Mock DiscoverRepoRootAsync for PackagePathParser.ParseAsync
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((string path, CancellationToken _) => path ?? "/test/repo");

        SetupSuccessfulProcessMocks();
        verifySetupService = new VerifySetupService(
            mockProcessHelper.Object,
            serviceLogger,
            _mockGitHelper.Object
        );
    }

    private void SetupSuccessfulProcessMocks()
    {
        mockProcessHelper
            .Setup(x => x.Run(
                    It.IsAny<ProcessOptions>(),
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
                    { "pip", "pip 24.0" },
                    { "java", "java 17.0.1" },
                    { "mvn", "Apache Maven 3.9.0" },
                    { "dotnet", "8.0.100" },
                    { "go", "go version go1.21.0" },
                    { "pnpm", "9.0.0" },
                    { "azpysdk", "azpysdk help" },
                    { "sdk_generator", "sdk_generator help" },
                    { "GitPython", "GitPython 3.1.0" },
                    { "pytest", "pytest 8.3.5" },
                    { "golangci-lint", "golangci-lint 1.55.0" },
                    { "goimports", "goimports" },
                    { "generator", "generator 0.4.3" }
                };
                foreach (var kvp in successfulCommands)
                {
                    var command = kvp.Key;
                    var output = kvp.Value;
                    if (processOptions.Command.Contains(command) ||
                        processOptions.Args.Any(a => a.Contains(command)))
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
                It.Is<ProcessOptions>(opt => opt.Command.Contains(command) || opt.Args.Any(a => a.Contains(command))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = exitCode,
                OutputDetails = new List<(StdioLevel, string)> { (StdioLevel.StandardError, errorOutput) }
            });
    }

    private void SetupVersionMismatchMock(string command, string version)
    {
        mockProcessHelper
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(opt => opt.Command.Contains(command) || opt.Args.Any(a => a.Contains(command))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = 0,
                OutputDetails = new List<(StdioLevel, string)> { (StdioLevel.StandardOutput, version) }
            });
    }

    // Helper to recreate the service after changing mocks (since Setup() already built it)
    private void RecreateService()
    {
        verifySetupService = new VerifySetupService(
            mockProcessHelper.Object,
            serviceLogger,
            _mockGitHelper.Object
        );
    }

    [Test]
    public async Task VerifySetup_Succeeds_WhenAllRequirementsMet()
    {
        var result = await verifySetupService.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path/python");

        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task VerifySetup_Fails_WhenCoreRequirementNotMet()
    {
        SetupFailedProcessMock("node", 1, "node: command not found");
        RecreateService();

        var result = await verifySetupService.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path/python");

        Assert.That(result.Results, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Results!.Any(r => r.Requirement.Contains("Node")), Is.True);
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task VerifySetup_Fails_WhenVersionRequirementNotMet()
    {
        SetupVersionMismatchMock("node", "v18.0.0");
        RecreateService();

        var result = await verifySetupService.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path/python");

        Assert.That(result.Results, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Results!.Any(r => r.Requirement.Contains("Node")), Is.True);
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task VerifySetup_OnlyChecksLanguageSpecificRequirements_ForSpecifiedLanguage()
    {
        SetupFailedProcessMock("java", 1, "java: command not found");
        RecreateService();

        var result = await verifySetupService.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path/python");

        Assert.That(result.Results?.Any(r => r.Requirement.Contains("Java")) ?? false, Is.False);
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task VerifySetup_ChecksCoreRequirements_ForAnyLanguage()
    {
        SetupFailedProcessMock("pwsh", 1, "pwsh: command not found");
        RecreateService();

        var result = await verifySetupService.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Go }, "/test/path/go");

        Assert.That(result.Results, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Results!.Any(r => r.Requirement.Contains("PowerShell")), Is.True);
    }

    [Test]
    public async Task VerifySetup_ReturnsInstructions_WhenRequirementFails()
    {
        SetupFailedProcessMock("node", 1, "node: command not found");
        RecreateService();

        var result = await verifySetupService.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path/python");

        Assert.That(result.Results, Is.Not.Null.And.Not.Empty);
        var nodeResult = result.Results!.FirstOrDefault(r => r.Requirement.Contains("Node"));
        Assert.That(nodeResult, Is.Not.Null);
        Assert.That(nodeResult!.Instructions, Is.Not.Empty);
    }

    [Test]
    public async Task VerifySetup_AutoInstall_SucceedsAndVerifies()
    {
        var tspCheckCount = 0;
        mockProcessHelper
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(opt => opt.Command.Contains("tsp") || opt.Args.Any(a => a == "tsp")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                tspCheckCount++;
                if (tspCheckCount <= 1)
                {
                    return new ProcessResult
                    {
                        ExitCode = 1,
                        OutputDetails = new List<(StdioLevel, string)> { (StdioLevel.StandardError, "tsp: command not found") }
                    };
                }
                return new ProcessResult
                {
                    ExitCode = 0,
                    OutputDetails = new List<(StdioLevel, string)> { (StdioLevel.StandardOutput, "1.0.1") }
                };
            });
        RecreateService();

        var result = await verifySetupService.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.DotNet }, "/test/path/dotnet", requirementsToInstall: new List<string> { "tsp" });

        Assert.That(result.ResponseError, Is.Null);
        var tspResult = result.Results?.FirstOrDefault(r => r.Requirement.Contains("tsp") && !r.Requirement.Contains("tsp-client"));
        Assert.That(tspResult, Is.Not.Null);
        Assert.That(tspResult!.AutoInstallAttempted, Is.True);
        Assert.That(tspResult.AutoInstallSucceeded, Is.True);
        Assert.That(tspResult.AutoInstallError, Is.Null);
    }

    [Test]
    public async Task VerifySetup_AutoInstall_InstallSucceeds_ButRecheckFails()
    {
        mockProcessHelper
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(opt => opt.Args.Any(a => a == "tsp") || opt.Command.Contains("tsp")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = 1,
                OutputDetails = new List<(StdioLevel, string)> { (StdioLevel.StandardError, "tsp: command not found") }
            });

        mockProcessHelper
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(opt => opt.Command.Contains("npm") && opt.Args.Any(a => a.Contains("@typespec/compiler"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = 0,
                OutputDetails = new List<(StdioLevel, string)> { (StdioLevel.StandardOutput, "added 1 package") }
            });
        RecreateService();

        var result = await verifySetupService.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.DotNet }, "/test/path/dotnet", requirementsToInstall: new List<string> { "tsp" });

        Assert.That(result.ResponseError, Is.Null);
        var tspResult = result.Results?.FirstOrDefault(r => r.Requirement.Contains("tsp") && !r.Requirement.Contains("tsp-client"));
        Assert.That(tspResult, Is.Not.Null);
        Assert.That(tspResult!.AutoInstallAttempted, Is.True);
        Assert.That(tspResult.AutoInstallSucceeded, Is.False);
        Assert.That(tspResult.AutoInstallError, Is.Not.Null.And.Not.Empty);
        Assert.That(tspResult.RequirementStatusDetails, Does.Contain("verification still fails"));
        Assert.That(tspResult.Instructions, Is.Not.Empty);
    }

    [Test]
    public async Task VerifySetup_AutoInstall_InstallCommandFails()
    {
        mockProcessHelper
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(opt => opt.Args.Any(a => a == "tsp") || opt.Command.Contains("tsp")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = 1,
                OutputDetails = new List<(StdioLevel, string)> { (StdioLevel.StandardError, "tsp: command not found") }
            });

        mockProcessHelper
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(opt => opt.Command.Contains("npm") && opt.Args.Any(a => a.Contains("@typespec/compiler"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = 1,
                OutputDetails = new List<(StdioLevel, string)> { (StdioLevel.StandardError, "npm ERR! install failed") }
            });
        RecreateService();

        var result = await verifySetupService.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.DotNet }, "/test/path/dotnet", requirementsToInstall: new List<string> { "tsp" });

        Assert.That(result.ResponseError, Is.Null);
        var tspResult = result.Results?.FirstOrDefault(r => r.Requirement.Contains("tsp") && !r.Requirement.Contains("tsp-client"));
        Assert.That(tspResult, Is.Not.Null);
        Assert.That(tspResult!.AutoInstallAttempted, Is.True);
        Assert.That(tspResult.AutoInstallSucceeded, Is.False);
        Assert.That(tspResult.AutoInstallError, Is.Not.Null);
    }

    [Test]
    public async Task VerifySetup_AutoInstall_NonInstallableRequirementReportsInstructions()
    {
        SetupFailedProcessMock("node", 1, "node: command not found");
        RecreateService();

        var result = await verifySetupService.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.DotNet }, "/test/path/dotnet", requirementsToInstall: new List<string> { "Node.js" });

        Assert.That(result.ResponseError, Is.Null);
        var nodeResult = result.Results?.FirstOrDefault(r => r.Requirement.Contains("Node"));
        Assert.That(nodeResult, Is.Not.Null);
        Assert.That(nodeResult!.AutoInstallAttempted, Is.False);
        Assert.That(nodeResult.IsAutoInstallable, Is.False);
        Assert.That(nodeResult.NotAutoInstallableReason, Is.Not.Null.And.Not.Empty);
        Assert.That(nodeResult.Instructions, Is.Not.Empty);
    }

    [Test]
    public async Task VerifySetup_SkipsDependents_WhenDependencyFails()
    {
        SetupFailedProcessMock("node", 1, "node: command not found");
        RecreateService();

        var result = await verifySetupService.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.DotNet }, "/test/path/dotnet");

        Assert.That(result.ResponseError, Is.Null);
        var nodeResult = result.Results?.FirstOrDefault(r => r.Requirement.Contains("Node"));
        Assert.That(nodeResult, Is.Not.Null);

        var tspClientResult = result.Results?.FirstOrDefault(r => r.Requirement.Contains("tsp-client"));
        Assert.That(tspClientResult, Is.Not.Null);
        Assert.That(tspClientResult!.RequirementStatusDetails, Does.Contain("Skipped"));
        Assert.That(tspClientResult.RequirementStatusDetails, Does.Contain("Node.js"));

        var tspResult = result.Results?.FirstOrDefault(r => r.Requirement.Contains("tsp") && !r.Requirement.Contains("tsp-client"));
        Assert.That(tspResult, Is.Not.Null);
        Assert.That(tspResult!.RequirementStatusDetails, Does.Contain("Skipped"));
        Assert.That(tspResult.RequirementStatusDetails, Does.Contain("Node.js"));
    }

    [Test]
    public async Task VerifySetup_AutoInstall_SkipsDependents_WhenDependencyFails()
    {
        SetupFailedProcessMock("node", 1, "node: command not found");
        RecreateService();

        var result = await verifySetupService.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.DotNet }, "/test/path/dotnet", requirementsToInstall: new List<string> { "tsp" });

        Assert.That(result.ResponseError, Is.Null);
        var tspResult = result.Results?.FirstOrDefault(r => r.Requirement.Contains("tsp") && !r.Requirement.Contains("tsp-client"));
        Assert.That(tspResult, Is.Not.Null);
        Assert.That(tspResult!.AutoInstallAttempted, Is.False);
        Assert.That(tspResult.RequirementStatusDetails, Does.Contain("Skipped"));
        Assert.That(tspResult.RequirementStatusDetails, Does.Contain("Node.js"));
    }

    [Test]
    public async Task VerifySetup_CheckOnly_WhenNoRequirementsToInstall()
    {
        SetupFailedProcessMock("tsp", 1, "tsp: command not found");
        RecreateService();

        var result = await verifySetupService.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.DotNet }, "/test/path/dotnet");

        Assert.That(result.ResponseError, Is.Null);
        var tspResult = result.Results?.FirstOrDefault(r => r.Requirement.Contains("tsp") && !r.Requirement.Contains("tsp-client"));
        Assert.That(tspResult, Is.Not.Null);
        Assert.That(tspResult!.AutoInstallAttempted, Is.False);
        Assert.That(tspResult.IsAutoInstallable, Is.True);
    }

    [Test]
    public async Task VerifySetup_IgnoresInvalidRequirementNames()
    {
        SetupFailedProcessMock("tsp", 1, "tsp: command not found");
        RecreateService();

        var result = await verifySetupService.VerifySetup(
            new HashSet<SdkLanguage> { SdkLanguage.DotNet },
            "/test/path/dotnet",
            requirementsToInstall: new List<string> { "nonexistent-tool", "fake-req" });

        Assert.That(result.ResponseError, Is.Null);
        var tspResult = result.Results?.FirstOrDefault(r => r.Requirement.Contains("tsp") && !r.Requirement.Contains("tsp-client"));
        Assert.That(tspResult, Is.Not.Null);
        Assert.That(tspResult!.AutoInstallAttempted, Is.False);
    }

    [Test]
    public async Task VerifySetup_OnlyInstallsRequestedRequirements()
    {
        SetupFailedProcessMock("tsp", 1, "tsp: command not found");

        mockProcessHelper
            .Setup(x => x.Run(
                It.Is<ProcessOptions>(opt => opt.Args.Any(a => a.Contains("tsp-client"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = 1,
                OutputDetails = new List<(StdioLevel, string)> { (StdioLevel.StandardError, "tsp-client: command not found") }
            });
        RecreateService();

        var result = await verifySetupService.VerifySetup(
            new HashSet<SdkLanguage> { SdkLanguage.DotNet },
            "/test/path/dotnet",
            requirementsToInstall: new List<string> { "tsp" });

        Assert.That(result.ResponseError, Is.Null);
        var tspResult = result.Results?.FirstOrDefault(r => r.Requirement.Contains("tsp") && !r.Requirement.Contains("tsp-client"));
        Assert.That(tspResult, Is.Not.Null);
        Assert.That(tspResult!.AutoInstallAttempted, Is.True);

        var tspClientResult = result.Results?.FirstOrDefault(r => r.Requirement.Contains("tsp-client"));
        Assert.That(tspClientResult, Is.Not.Null);
        Assert.That(tspClientResult!.AutoInstallAttempted, Is.False);
    }
}
