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
        private CheckAllTool _checkAllTool;
        private string _testProjectPath;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<CheckAllTool>>();
            _mockOutputService = new Mock<IOutputService>();
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
            Assert.AreEqual("All checks completed successfully", result.Message);
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
        public async Task RunAllChecks_WithSelectiveChecks_RunsOnlySelectedChecks()
        {
            // Arrange
            var options = new CheckOptions
            {
                SpellCheck = true,
                LinkValidation = true,
                ReadmeValidation = false,
                DependencyCheck = false,
                ChangelogValidation = false,
                SnippetUpdate = false
            };

            // Act
            var result = await _checkAllTool.RunAllChecks(_testProjectPath, options);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.ResponseError);
            
            // Verify that only 2 checks ran (spell check and link validation)
            var checkResults = result.Result as System.Collections.Generic.List<CheckResult>;
            Assert.IsNotNull(checkResults);
            Assert.AreEqual(2, checkResults.Count);
            Assert.IsTrue(checkResults.Any(r => r.CheckType == "Spell Check"));
            Assert.IsTrue(checkResults.Any(r => r.CheckType == "Link Validation"));
        }
    }
}