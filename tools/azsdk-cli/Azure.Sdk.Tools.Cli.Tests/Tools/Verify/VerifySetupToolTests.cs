// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.SetupRequirements;
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
    private Mock<ICopilotAgentRunner> _mockMicrohostAgent;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<ICommonValidationHelpers> _commonValidationHelpers;
    private IPackageInfoHelper _packageInfoHelper;

    [SetUp]
    public void Setup()
    {
        mockProcessHelper = new Mock<IProcessHelper>();
        mockPythonHelper = new Mock<IPythonHelper>();
        logger = new TestLogger<VerifySetupTool>();

        _languageLogger = new TestLogger<LanguageService>();
        _mockMicrohostAgent = new Mock<ICopilotAgentRunner>();
        _mockNpxHelper = new Mock<INpxHelper>();
        _mockPowerShellHelper = new Mock<IPowershellHelper>();
        _mockGitHelper = new Mock<IGitHelper>();
        _commonValidationHelpers = new Mock<ICommonValidationHelpers>();
        _packageInfoHelper = new PackageInfoHelper(new TestLogger<PackageInfoHelper>(), _mockGitHelper.Object);

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

        languageServices = [
            new PythonLanguageService(mockProcessHelper.Object, mockPythonHelper.Object, _mockNpxHelper.Object, _mockGitHelper.Object, _languageLogger, _commonValidationHelpers.Object, _packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            new JavaLanguageService(mockProcessHelper.Object, _mockGitHelper.Object, new Mock<IMavenHelper>().Object, _mockMicrohostAgent.Object, _languageLogger, _commonValidationHelpers.Object, _packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            new JavaScriptLanguageService(mockProcessHelper.Object, _mockNpxHelper.Object, _mockGitHelper.Object, _languageLogger, _commonValidationHelpers.Object, _packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            new GoLanguageService(mockProcessHelper.Object, _mockPowerShellHelper.Object, _mockGitHelper.Object, _languageLogger, _commonValidationHelpers.Object, _packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            new DotnetLanguageService(mockProcessHelper.Object, _mockPowerShellHelper.Object, _mockGitHelper.Object, _languageLogger, _commonValidationHelpers.Object, _packageInfoHelper, Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>())
        ];

        SetupSuccessfulProcessMocks();
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

    [Test]
    public async Task VerifySetup_Succeeds_WhenAllRequirementsMet()
    {
        // Arrange
        var tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            _mockGitHelper.Object,
            _packageInfoHelper,
            languageServices
        );

        // Act - Check Python requirements
        var result = await tool.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path/python");

        // Assert - Should have no failures when all mocks return success
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task VerifySetup_Fails_WhenCoreRequirementNotMet()
    {
        // Arrange - Node.js command fails
        SetupFailedProcessMock("node", 1, "node: command not found");

        var tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            _mockGitHelper.Object,
            _packageInfoHelper,
            languageServices
        );

        // Act
        var result = await tool.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path/python");

        // Assert
        Assert.That(result.Results, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Results!.Any(r => r.Requirement.Contains("Node")), Is.True);
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task VerifySetup_Fails_WhenVersionRequirementNotMet()
    {
        // Arrange - Node.js returns old version
        SetupVersionMismatchMock("node", "v18.0.0");

        var tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            _mockGitHelper.Object,
            _packageInfoHelper,
            languageServices
        );

        // Act
        var result = await tool.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path/python");

        // Assert - Should have a failure for Node.js version
        Assert.That(result.Results, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Results!.Any(r => r.Requirement.Contains("Node")), Is.True);
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task VerifySetup_OnlyChecksLanguageSpecificRequirements_ForSpecifiedLanguage()
    {
        // Arrange - Java command fails, but we only request Python
        SetupFailedProcessMock("java", 1, "java: command not found");

        var tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            _mockGitHelper.Object,
            _packageInfoHelper,
            languageServices
        );

        // Act - Only request Python
        var result = await tool.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path/python");

        // Assert - Should not fail for Java requirements since we only requested Python
        Assert.That(result.Results?.Any(r => r.Requirement.Contains("Java")) ?? false, Is.False);
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task VerifySetup_ChecksCoreRequirements_ForAnyLanguage()
    {
        // Arrange - PowerShell command fails
        SetupFailedProcessMock("pwsh", 1, "pwsh: command not found");

        var tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            _mockGitHelper.Object,
            _packageInfoHelper,
            languageServices
        );

        // Act - Request any language
        var result = await tool.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Go }, "/test/path/go");

        // Assert - Should fail for PowerShell since it's a core requirement
        Assert.That(result.Results, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Results!.Any(r => r.Requirement.Contains("PowerShell")), Is.True);
    }

    [Test]
    public async Task VerifySetup_ReturnsInstructions_WhenRequirementFails()
    {
        // Arrange - Node.js command fails
        SetupFailedProcessMock("node", 1, "node: command not found");

        var tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            _mockGitHelper.Object,
            _packageInfoHelper,
            languageServices
        );

        // Act
        var result = await tool.VerifySetup(new HashSet<SdkLanguage> { SdkLanguage.Python }, "/test/path/python");

        // Assert - Should include installation instructions
        Assert.That(result.Results, Is.Not.Null.And.Not.Empty);
        var nodeResult = result.Results!.FirstOrDefault(r => r.Requirement.Contains("Node"));
        Assert.That(nodeResult, Is.Not.Null);
        Assert.That(nodeResult!.Instructions, Is.Not.Empty);
    }

    [Test]
    public void LanguagesParam_RejectsUnknownLanguages()
    {
        var tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            _mockGitHelper.Object,
            _packageInfoHelper,
            languageServices
        );

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
        var tool = new VerifySetupTool(
            mockProcessHelper.Object,
            logger,
            _mockGitHelper.Object,
            _packageInfoHelper,
            languageServices
        );

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
