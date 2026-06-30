using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.ClassifyItems;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using GitHub.Copilot.SDK;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    public class ClassifyServiceTests
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
        private TestLogger<SdkBuildTool> _logger;
        private TempDirectory _tempDirectory;
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
            _mockNpxHelper = new Mock<INpxHelper>();
            _mockPowerShellHelper = new Mock<IPowershellHelper>();
            _logger = new TestLogger<SdkBuildTool>();
            _commonValidationHelpers = new Mock<ICommonValidationHelpers>();
            _mockAgentRunner = new Mock<ICopilotAgentRunner>();
            _mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            _mockLoggerFactory = new Mock<ILoggerFactory>();
            _mockFeedbackService = new Mock<IAPIViewFeedbackService>();
            _mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>()))
                .Returns(new TestLogger<FeedbackClassifierService>());

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

        private ClassifyService CreateMockedService()
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
            return new ClassifyService(
                copilotAgentRunner);
        }
        #endregion
        #region Classify SDK Breaking Changes Tests
        [Test]
        [TestCase("### Breaking Changes\n\n- Field `Tier` of struct `ResourceSKU` has been removed\n\n### Features Added\n\n- New field `TierNew` in struct `ResourceSKU`\n", SdkLanguage.Go, "azure-sdk-for-go")]
        //[TestCase("", SdkLanguage.Python, "azure-sdk-for-python")]
        //[TestCase("", SdkLanguage.Java, "azure-sdk-for-java")]
        //[TestCase("", SdkLanguage.JavaScript, "azure-sdk-for-js")]
        public async Task ClassifySDKBreakingChanges_RenameProperty(string sdkchanges, SdkLanguage language, string sdkRepoName)
        {
            var sdkRepoRoot = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "SdkPackageTestData",
                sdkRepoName);
            var service = CreateMockedService();
            var languageService = _languageServices.First(s => s.Language == language);
            var ct = CancellationToken.None;
            var sdkBreakingPattern = await languageService.GetSDKBreakingPattern(sdkRepoRoot, ct);
            var tspProjectFile = Path.Combine(_typeSpecProjectPath, "tspconfig.yaml");
            var classifyRequest = new ClassifySdkBreakingChangesRequest(sdkchanges, sdkRepoRoot, sdkBreakingPattern, languageService.Language.ToString(), tspProjectFile);
            var classifyResult = await service.ClassifyItemsAsync(ClassifyType.SdkBreakingChange, classifyRequest, ct);
            Assert.IsNotNull(classifyRequest);
            var result = classifyResult.ClassifiedResult;
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<Array>(result);
            Assert.That((result as Array), Has.Length.EqualTo(1));
        }
        #endregion

    }
}
