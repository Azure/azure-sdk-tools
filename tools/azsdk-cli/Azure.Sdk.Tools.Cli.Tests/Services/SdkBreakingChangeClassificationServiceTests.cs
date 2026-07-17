// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    public class SdkBreakingChangeClassificationServiceTests
    {
        private Mock<ICopilotAgentRunner> _mockAgentRunner = null!;
        private Mock<ITypeSpecHelper> _mockTypeSpecHelper = null!;
        private Mock<ILoggerFactory> _mockLoggerFactory = null!;
        private Mock<IAPIViewFeedbackService> _mockFeedbackService = null!;
        private string _testTspPath = null!;
        private string _specRepoRoot = null!;
        private string _typeSpecProjectPath = null!;

        private Mock<IGitHelper> _mockGitHelper;
        private Mock<IProcessHelper> _mockProcessHelper;
        private Mock<IPythonHelper> _mockPythonHelper;
        private Mock<INpxHelper> _mockNpxHelper;
        private Mock<IPowershellHelper> _mockPowerShellHelper;
        private Mock<ISpecGenSdkConfigHelper> _mockSpecGenSdkConfigHelper;
        private TestLogger<SdkBreakingChangeClassificationService> logger;
        private List<LanguageService> _languageServices;
        private Mock<ICommonValidationHelpers> _commonValidationHelpers;
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Path to the test TypeSpec project for live tests
            _typeSpecProjectPath = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "SdkPackageTestData",
                "TestDetectSdkBreakingchanges",
                "specification",
                "Contoso.Management");

            // Create the customization guide file that live tests require
            // The TypeSpecHelper looks for this at <specRepoRoot>/eng/common/knowledge/customizing-client-tsp.md
            var testDataRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "TypeSpecTestData");
            var liveTestGuidePath = Path.Combine(testDataRoot, "eng", "common", "knowledge", "customizing-client-tsp.md");
            Directory.CreateDirectory(Path.GetDirectoryName(liveTestGuidePath)!);
            if (!File.Exists(liveTestGuidePath))
            {
                File.WriteAllText(liveTestGuidePath, "# TypeSpec Client Customizations\nTest reference content for live tests.");
            }
        }

        [SetUp]
        public void Setup()
        {
            // Create mocks
            _mockGitHelper = new Mock<IGitHelper>();
            _mockProcessHelper = new Mock<IProcessHelper>();
            _mockPythonHelper = new Mock<IPythonHelper>();
            _mockPythonHelper
                .Setup(x => x.Run(It.IsAny<PythonOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0 });
            _mockSpecGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();
            _mockSpecGenSdkConfigHelper.Setup(x => x.GetSdkBreakingChangePatternFileConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                        .Returns<string, CancellationToken>((path, ct) => Task.FromResult("documentation/development/breaking-changes/sdk-breaking-changes-guide-migration.md"));
            _mockNpxHelper = new Mock<INpxHelper>();
            _mockPowerShellHelper = new Mock<IPowershellHelper>();
            logger = new TestLogger<SdkBreakingChangeClassificationService>();
            _commonValidationHelpers = new Mock<ICommonValidationHelpers>();
            _mockAgentRunner = new Mock<ICopilotAgentRunner>();
            _mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            _mockLoggerFactory = new Mock<ILoggerFactory>();
            _mockFeedbackService = new Mock<IAPIViewFeedbackService>();
            _mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>()))
                .Returns(new TestLogger<SdkBreakingChangeClassificationService>());

            // Set up a fake tsp project path for mocked tests
            _specRepoRoot = Path.Combine(Path.GetTempPath(), "test-spec-repo-" + Guid.NewGuid().ToString("N")[..8]);
            _testTspPath = Path.Combine(_specRepoRoot, "specification", "widget", "Widget.Management");

            // Mock the spec repo root detection
            _mockTypeSpecHelper.Setup(x => x.GetSpecRepoRootPath(_testTspPath)).Returns(_specRepoRoot);

            // Create the customization guide file that the service expects
            var guidePath = Path.Combine(_specRepoRoot, "eng", "common", "knowledge", "customizing-client-tsp.md");
            Directory.CreateDirectory(Path.GetDirectoryName(guidePath)!);
            File.WriteAllText(guidePath, "# TypeSpec Client Customizations\nTest reference content.");
            var languageLogger = new TestLogger<LanguageService>();
            var packageInfoHelper = new PackageInfoHelper(new TestLogger<PackageInfoHelper>(), _mockGitHelper.Object);

            _languageServices = [
                new PythonLanguageService(_mockProcessHelper.Object, _mockPythonHelper.Object, _mockNpxHelper.Object, Mock.Of<ICopilotAgentRunner>(), _mockGitHelper.Object, languageLogger, _commonValidationHelpers.Object, packageInfoHelper, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>()),
                new JavaLanguageService(_mockProcessHelper.Object, _mockGitHelper.Object, new Mock<IMavenHelper>().Object, _mockPythonHelper.Object, _mockAgentRunner.Object, languageLogger, _commonValidationHelpers.Object, packageInfoHelper, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>()),
                new JavaScriptLanguageService(_mockProcessHelper.Object, _mockNpxHelper.Object, Mock.Of<ICopilotAgentRunner>(), _mockGitHelper.Object, languageLogger, _commonValidationHelpers.Object, packageInfoHelper, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>()),
                new GoLanguageService(_mockProcessHelper.Object, _mockPowerShellHelper.Object, _mockGitHelper.Object, languageLogger, _commonValidationHelpers.Object, packageInfoHelper, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>()),
                new DotnetLanguageService(_mockProcessHelper.Object, _mockPowerShellHelper.Object, Mock.Of<ICopilotAgentRunner>(), _mockGitHelper.Object, languageLogger, _commonValidationHelpers.Object, packageInfoHelper, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>()),
                new RustLanguageService(_mockProcessHelper.Object, _mockPowerShellHelper.Object, _mockGitHelper.Object, languageLogger, _commonValidationHelpers.Object, packageInfoHelper, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>())
            ];
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up temp files
            if (Directory.Exists(_specRepoRoot))
            {
                try { Directory.Delete(_specRepoRoot, recursive: true); } catch { }
            }
        }

        #region Mocked Test Helpers
        private SdkBreakingChangeClassificationService CreateMockedService()
        {
            return new SdkBreakingChangeClassificationService(
                _mockAgentRunner.Object, logger);
        }
        #endregion

        #region Live Test Helpers
        private SdkBreakingChangeClassificationService CreateRealService()
        {
            var rawOutputHelper = Mock.Of<IRawOutputHelper>();
            var copilotClient = new CopilotClient(new CopilotClientOptions
            {
                UseStdio = true,
                AutoStart = true
            });
            var copilotClientWrapper = new CopilotClientWrapper(copilotClient);
            var tokenUsageHelper = new TokenUsageHelper(rawOutputHelper);
            var copilotAgentRunner = new CopilotAgentRunner(
                copilotClientWrapper,
                tokenUsageHelper,
                new TestLogger<CopilotAgentRunner>());
            return new SdkBreakingChangeClassificationService(
                copilotAgentRunner, logger);
        }
        private static GitHelper CreateRealGitHelper()
        {
            var rawOutputHelper = Mock.Of<IRawOutputHelper>();
            var gitCommandHelper = new GitCommandHelper(
                new TestLogger<GitCommandHelper>(),
                rawOutputHelper);
            return new GitHelper(
                Mock.Of<IGitHubService>(),
                gitCommandHelper,
                new TestLogger<GitHelper>());
        }
        #endregion
        #region Classify SDK Breaking Changes Tests
        [Test, Explicit]
        [Category(TestCategories.CopilotAgent)]
        [TestCase("### Breaking Changes\n\n- Field `Tier` of struct `ResourceSKU` has been removed\n\n### Features Added\n\n- New field `GroupPresenceEvents` in struct `EventHandler`\n- New field `TierNew` in struct `ResourceSKU`\n", SdkLanguage.Go, "azure-sdk-for-go")]
        public async Task ClassifySDKBreakingChanges_RenameProperty(string sdkchanges, SdkLanguage language, string sdkRepoName)
        {
            var sdkRepoRoot = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "SdkPackageTestData",
                sdkRepoName);
            var service = CreateRealService();
            var languageService = _languageServices.First(s => s.Language == language);
            var ct = CancellationToken.None;
            var sdkBreakingPattern = await languageService.GetSdkBreakingPattern(sdkRepoRoot, ct);
            var tspProjectFile = Path.Combine(_typeSpecProjectPath, "tspconfig.yaml");
            var classifyResult = await service.ClassifySdkBreakingChangesAsync(sdkchanges, sdkBreakingPattern, languageService.Language.ToString(), _typeSpecProjectPath, ct);
            Assert.IsNotNull(classifyResult);
            Assert.IsInstanceOf<List<SdkBreakingChange>>(classifyResult);
            Assert.That(classifyResult, Has.Count.EqualTo(1));
        }
        #endregion

        #region Passer AI result Tests
        [Test]
        public async Task ClassifySDKBreakingChanges_MultipleLine()
        {
            var service = CreateMockedService();
            var batchResponse = """
            [rename model]
            breaking: Model WebPubSubResource renamed to Resource.

            This affects request payloads
            and response payloads.
            category: conversion-need resolve
            resolution:
            Use @clientName to in client.tsp to restore the original SDK name.

            originBreaks:
            - break1
            - break2
            [method parameter order changed]
            breaking: The Function parameter order changed.
            category:spec-change
            resolution: use @override to restore the original parameter orders
            originBreaks:- breakA
            - breakB
            """;
            _mockAgentRunner
            .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

            var result = await service.ClassifySdkBreakingChangesAsync("test sdk changes", "test pattern", "Go", _typeSpecProjectPath, CancellationToken.None);
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<List<SdkBreakingChange>>(result);
            Assert.That(result, Has.Count.EqualTo(2));
            var firstChange = result[0];
            Assert.That(firstChange.BreakingChange, Is.EqualTo("""
                Model WebPubSubResource renamed to Resource.
                
                This affects request payloads
                and response payloads.
                """));
            Assert.That(firstChange.Category, Is.EqualTo("conversion-need resolve"));
            Assert.That(firstChange.Resolution, Is.EqualTo("Use @clientName to in client.tsp to restore the original SDK name."));
            Assert.IsNotNull(firstChange.OriginBreaks);
            Assert.That(firstChange.OriginBreaks, Has.Count.EqualTo(2));
            Assert.That(firstChange.OriginBreaks[0], Does.Contain("break1"));
            Assert.That(firstChange.OriginBreaks[1], Does.Contain("break2"));
            var secondChange = result[1];
            Assert.That(secondChange.BreakingChange, Is.EqualTo("The Function parameter order changed."));
            Assert.That(secondChange.Category, Is.EqualTo("spec-change"));
            Assert.That(secondChange.Resolution, Is.EqualTo("use @override to restore the original parameter orders"));
            Assert.IsNotNull(secondChange.OriginBreaks);
            Assert.That(secondChange.OriginBreaks, Has.Count.EqualTo(2));
            Assert.That(secondChange.OriginBreaks[0], Does.Contain("breakA"));
            Assert.That(secondChange.OriginBreaks[1], Does.Contain("breakB"));
        }

        [Test]
        public async Task ClassifySDKBreakingChanges_ContentContainsLabel()
        {
            var service = CreateMockedService();
            var batchResponse = """
            [rename model]
            breaking: Model WebPubSubResource renamed to Resource.

            This affects request payloads and category
            and response payloads.
            category: conversion-need resolve
            resolution:
            Use @clientName to in client.tsp to restore the original SDK name.

            originBreaks:
            - break1
            - break2
            """;
            _mockAgentRunner
            .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

            var result = await service.ClassifySdkBreakingChangesAsync("test sdk changes", "test pattern", "Go", _typeSpecProjectPath, CancellationToken.None);
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<List<SdkBreakingChange>>(result);
            Assert.That(result, Has.Count.EqualTo(1));
            var firstChange = result[0];
            Assert.That(firstChange.BreakingChange, Is.EqualTo("""
                Model WebPubSubResource renamed to Resource.
                
                This affects request payloads and category
                and response payloads.
                """));
            Assert.That(firstChange.Category, Is.EqualTo("conversion-need resolve"));
            Assert.That(firstChange.Resolution, Is.EqualTo("Use @clientName to in client.tsp to restore the original SDK name."));
            Assert.IsNotNull(firstChange.OriginBreaks);
            Assert.That(firstChange.OriginBreaks, Has.Count.EqualTo(2));
            Assert.That(firstChange.OriginBreaks[0], Does.Contain("break1"));
            Assert.That(firstChange.OriginBreaks[1], Does.Contain("break2"));
        }

        [Test]
        public async Task ClassifySDKBreakingChanges_EmptyResponse()
        {
            var service = CreateMockedService();
            _mockAgentRunner
           .Setup(x => x.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync("");

            var result = await service.ClassifySdkBreakingChangesAsync("test sdk changes", "test pattern", "Go", _typeSpecProjectPath, CancellationToken.None);
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<List<SdkBreakingChange>>(result);
            Assert.That(result, Has.Count.EqualTo(0));
        }
        #endregion
    }
}
