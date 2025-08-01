using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.CheckAllTool;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    [TestFixture]
    public class CheckAllToolTests
    {
        private Mock<ILogger<CheckAllTool>> _mockLogger;
        private Mock<IOutputService> _mockOutputService;
        private Mock<ILogger<SpellCheckTool>> _mockSpellCheckLogger;
        private Mock<ILogger<LinkValidationTool>> _mockLinkValidationLogger;
        private Mock<ILogger<ReadmeValidationTool>> _mockReadmeValidationLogger;
        private Mock<ILogger<DependencyCheckTool>> _mockDependencyCheckLogger;
        private Mock<ILogger<ChangelogValidationTool>> _mockChangelogValidationLogger;
        private Mock<ILogger<SnippetUpdateTool>> _mockSnippetUpdateLogger;
        private Mock<ILogger<SpellCheckFixTool>> _mockSpellCheckFixLogger;
        private Mock<ILogger<LinkValidationFixTool>> _mockLinkValidationFixLogger;
        private Mock<ILogger<ReadmeValidationFixTool>> _mockReadmeValidationFixLogger;
        private Mock<ILogger<DependencyCheckFixTool>> _mockDependencyCheckFixLogger;
        private Mock<ILogger<ChangelogValidationFixTool>> _mockChangelogValidationFixLogger;
        private Mock<ILogger<SnippetUpdateFixTool>> _mockSnippetUpdateFixLogger;
        private CheckAllTool _checkAllTool;
        private string _testProjectPath;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<CheckAllTool>>();
            _mockOutputService = new Mock<IOutputService>();
            _mockSpellCheckLogger = new Mock<ILogger<SpellCheckTool>>();
            _mockLinkValidationLogger = new Mock<ILogger<LinkValidationTool>>();
            _mockReadmeValidationLogger = new Mock<ILogger<ReadmeValidationTool>>();
            _mockDependencyCheckLogger = new Mock<ILogger<DependencyCheckTool>>();
            _mockChangelogValidationLogger = new Mock<ILogger<ChangelogValidationTool>>();
            _mockSnippetUpdateLogger = new Mock<ILogger<SnippetUpdateTool>>();
            _mockSpellCheckFixLogger = new Mock<ILogger<SpellCheckFixTool>>();
            _mockLinkValidationFixLogger = new Mock<ILogger<LinkValidationFixTool>>();
            _mockReadmeValidationFixLogger = new Mock<ILogger<ReadmeValidationFixTool>>();
            _mockDependencyCheckFixLogger = new Mock<ILogger<DependencyCheckFixTool>>();
            _mockChangelogValidationFixLogger = new Mock<ILogger<ChangelogValidationFixTool>>();
            _mockSnippetUpdateFixLogger = new Mock<ILogger<SnippetUpdateFixTool>>();

            _checkAllTool = new CheckAllTool(_mockLogger.Object, _mockOutputService.Object);
            
            // Create a temporary test directory
            _testProjectPath = Path.Combine(Path.GetTempPath(), "CheckAllToolTest");
            if (Directory.Exists(_testProjectPath))
            {
                Directory.Delete(_testProjectPath, true);
            }
            Directory.CreateDirectory(_testProjectPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testProjectPath))
            {
                Directory.Delete(_testProjectPath, true);
            }
        }

        [Test]
        public async Task RunAllChecks_WithValidPath_ReturnsSuccessfulResult()
        {
            // Act
            var result = await _checkAllTool.RunAllChecks(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(string.IsNullOrEmpty(result.Message));
            Assert.IsNull(result.ResponseError);
            Assert.That(result.Message, Is.EqualTo("All checks completed successfully"));
        }

        [Test]
        public async Task RunAllChecks_WithInvalidPath_ReturnsErrorResult()
        {
            // Arrange
            string invalidPath = "/tmp/nonexistent-path-12345";

            // Act
            var result = await _checkAllTool.RunAllChecks(invalidPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.ResponseError);
            Assert.IsTrue(result.ResponseError.Contains("Project path does not exist"));
        }

        [Test]
        public async Task RunAllChecks_WithValidPath_RunsAllChecks()
        {
            // Act
            var result = await _checkAllTool.RunAllChecks(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.ResponseError);
            
            // Verify that all 6 checks ran
            var checkResults = result.Result as System.Collections.Generic.List<CheckResult>;
            Assert.IsNotNull(checkResults);
            Assert.That(checkResults.Count, Is.EqualTo(6));
            Assert.IsTrue(checkResults.Any(r => r.CheckType == "Spell Check"));
            Assert.IsTrue(checkResults.Any(r => r.CheckType == "Link Validation"));
            Assert.IsTrue(checkResults.Any(r => r.CheckType == "README Validation"));
            Assert.IsTrue(checkResults.Any(r => r.CheckType == "Dependency Check"));
            Assert.IsTrue(checkResults.Any(r => r.CheckType == "Changelog Validation"));
            Assert.IsTrue(checkResults.Any(r => r.CheckType == "Snippet Update"));
        }

        [Test]
        public async Task SpellCheckFixTool_WithValidPath_ReturnsSuccessfulResult()
        {
            // Arrange
            var fixTool = new SpellCheckFixTool(_mockSpellCheckFixLogger.Object);

            // Act
            var result = await fixTool.FixSpellCheckValidation(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.ResponseError);
            Assert.IsTrue(result.Message.Contains("Spell check fixes completed"));
        }

        [Test]
        public async Task LinkValidationFixTool_WithValidPath_ReturnsSuccessfulResult()
        {
            // Arrange
            var fixTool = new LinkValidationFixTool(_mockLinkValidationFixLogger.Object);

            // Act
            var result = await fixTool.FixLinkValidation(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.ResponseError);
            Assert.IsTrue(result.Message.Contains("Link validation fixes completed"));
        }

        [Test]
        public async Task ReadmeValidationFixTool_WithValidPath_ReturnsSuccessfulResult()
        {
            // Arrange
            var fixTool = new ReadmeValidationFixTool(_mockReadmeValidationFixLogger.Object);

            // Act
            var result = await fixTool.FixReadmeValidation(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.ResponseError);
            Assert.IsTrue(result.Message.Contains("README validation fixes completed"));
        }

        [Test]
        public async Task DependencyCheckFixTool_WithValidPath_ReturnsSuccessfulResult()
        {
            // Arrange
            var fixTool = new DependencyCheckFixTool(_mockDependencyCheckFixLogger.Object);

            // Act
            var result = await fixTool.FixDependencyCheckValidation(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.ResponseError);
            Assert.IsTrue(result.Message.Contains("Dependency check fixes completed"));
        }

        [Test]
        public async Task ChangelogValidationFixTool_WithValidPath_ReturnsSuccessfulResult()
        {
            // Arrange
            var fixTool = new ChangelogValidationFixTool(_mockChangelogValidationFixLogger.Object);

            // Act
            var result = await fixTool.FixChangelogValidation(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.ResponseError);
            Assert.IsTrue(result.Message.Contains("Changelog validation fixes completed"));
        }

        [Test]
        public async Task SnippetUpdateFixTool_WithValidPath_ReturnsSuccessfulResult()
        {
            // Arrange
            var fixTool = new SnippetUpdateFixTool(_mockSnippetUpdateFixLogger.Object);

            // Act
            var result = await fixTool.FixSnippetUpdateValidation(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.ResponseError);
            Assert.IsTrue(result.Message.Contains("Snippet update fixes completed"));
        }

        [Test]
        public async Task FixTools_WithInvalidPath_ReturnsErrorResult()
        {
            // Arrange
            string invalidPath = "/tmp/nonexistent-path-12345";
            var spellCheckFixTool = new SpellCheckFixTool(_mockSpellCheckFixLogger.Object);
            var linkValidationFixTool = new LinkValidationFixTool(_mockLinkValidationFixLogger.Object);

            // Act
            var spellCheckResult = await spellCheckFixTool.FixSpellCheckValidation(invalidPath);
            var linkValidationResult = await linkValidationFixTool.FixLinkValidation(invalidPath);

            // Assert
            Assert.IsNotNull(spellCheckResult.ResponseError);
            Assert.IsTrue(spellCheckResult.ResponseError.Contains("Project path does not exist"));
            Assert.IsNotNull(linkValidationResult.ResponseError);
            Assert.IsTrue(linkValidationResult.ResponseError.Contains("Project path does not exist"));
        }
    }
}