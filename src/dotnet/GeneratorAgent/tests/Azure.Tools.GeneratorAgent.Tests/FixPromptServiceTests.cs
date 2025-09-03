using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    internal class FixPromptServiceTests
    {
        #region Constructor Tests

        [Test]
        public void Constructor_WithValidLogger_ShouldCreateInstance()
        {
            var mockLogger = new Mock<ILogger<FixPromptService>>();
            var appSettings = CreateAppSettings();

            var service = new FixPromptService(mockLogger.Object, appSettings);

            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var appSettings = CreateAppSettings();
            var ex = Assert.Throws<ArgumentNullException>(() => new FixPromptService(null!, appSettings));
            Assert.That(ex!.ParamName, Is.EqualTo("logger"));
        }

        #endregion

        #region ConvertFixToPrompt Tests

        [Test]
        public void ConvertFixToPrompt_WithNullFix_ShouldThrowArgumentNullException()
        {
            var service = CreateFixPromptService();

            var ex = Assert.Throws<ArgumentNullException>(() => service.ConvertFixToPrompt(null!));
            Assert.That(ex!.ParamName, Is.EqualTo("fix"));
        }

        [Test]
        public void ConvertFixToPrompt_WithAgentPromptFixFromAnalyzer_ShouldReturnValidPrompt()
        {
            var service = CreateFixPromptService();
            var fix = CreateValidFixFromAnalyzer();

            var result = service.ConvertFixToPrompt(fix);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.Not.Empty);
                Assert.That(result, Does.Contain("### SPECIFIC FIX TO APPLY"));
                Assert.That(result, Does.Contain("### CONTEXT"));
                Assert.That(result, Does.Contain("SYSTEM INSTRUCTIONS"));
                Assert.That(result, Does.Contain("TypeSpec files and produce a valid, compilable result"));
                Assert.That(result, Does.Contain("FileSearchTool"));
                Assert.That(result, Does.Contain("Now apply this fix following the system instructions above"));
            });
        }

        [Test]
        public void ConvertFixToPrompt_WithAgentPromptFix_ShouldReturnValidPrompt()
        {
            var service = CreateFixPromptService();
            var fix = CreateValidAgentPromptFix();

            var result = service.ConvertFixToPrompt(fix);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.Not.Empty);
                Assert.That(result, Does.Contain("### SPECIFIC FIX TO APPLY"));
                Assert.That(result, Does.Contain("### CONTEXT"));
                Assert.That(result, Does.Contain("SYSTEM INSTRUCTIONS"));
                Assert.That(result, Does.Contain("TypeSpec files and produce a valid, compilable result"));
                Assert.That(result, Does.Contain("FileSearchTool"));
                Assert.That(result, Does.Contain("Now apply this fix following the system instructions above"));
            });
        }

        [Test]
        public void ConvertFixToPrompt_WithAgentPromptFixWithoutContext_ShouldReturnValidPrompt()
        {
            var service = CreateFixPromptService();
            var fix = CreateAgentPromptFixWithoutContext();

            var result = service.ConvertFixToPrompt(fix);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.Not.Empty);
                Assert.That(result, Does.Contain("### SPECIFIC FIX TO APPLY"));
                Assert.That(result, Does.Contain("### CONTEXT"));
                Assert.That(result, Does.Contain("No additional context provided"));
            });
        }

        [Test]
        public void ConvertFixToPrompt_WithDifferentFixTypes_ShouldProduceConsistentStructure()
        {
            var service = CreateFixPromptService();
            var analyzerFix = CreateValidFixFromAnalyzer();
            var agentPromptFix = CreateValidAgentPromptFix();

            var analyzerResult = service.ConvertFixToPrompt(analyzerFix);
            var agentPromptResult = service.ConvertFixToPrompt(agentPromptFix);

            Assert.Multiple(() =>
            {
                Assert.That(analyzerResult, Does.Contain("### SPECIFIC FIX TO APPLY"));
                Assert.That(agentPromptResult, Does.Contain("### SPECIFIC FIX TO APPLY"));
                Assert.That(analyzerResult, Does.Contain("### CONTEXT"));
                Assert.That(agentPromptResult, Does.Contain("### CONTEXT"));
                Assert.That(analyzerResult, Does.Contain("SYSTEM INSTRUCTIONS"));
                Assert.That(agentPromptResult, Does.Contain("SYSTEM INSTRUCTIONS"));
            });
        }

        #endregion

        #region Performance Tests

        [Test]
        public void ConvertFixToPrompt_MultipleCallsWithSameFix_ShouldProduceConsistentResults()
        {
            var service = CreateFixPromptService();
            var fix = CreateValidFixFromAnalyzer();

            var result1 = service.ConvertFixToPrompt(fix);
            var result2 = service.ConvertFixToPrompt(fix);

            Assert.That(result1, Is.EqualTo(result2));
        }

        [Test]
        public void ConvertFixToPrompt_WithMultipleFixes_ShouldCompleteInReasonableTime()
        {
            var service = CreateFixPromptService();
            var fixes = CreateMultipleValidFixes();

            var startTime = DateTime.UtcNow;
            foreach (var fix in fixes)
            {
                service.ConvertFixToPrompt(fix);
            }
            var endTime = DateTime.UtcNow;

            var duration = endTime - startTime;
            Assert.That(duration.TotalMilliseconds, Is.LessThan(1000), "Should complete 10 fixes in under 1 second");
        }

        #endregion

        #region Helper Methods

        private FixPromptService CreateFixPromptService()
        {
            var mockLogger = new Mock<ILogger<FixPromptService>>();
            var appSettings = CreateAppSettings();
            return new FixPromptService(mockLogger.Object, appSettings);
        }

        private AppSettings CreateAppSettings()
        {
            var mockConfiguration = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<AppSettings>>();
            
            // Set up required configuration values
            var projectEndpointSection = new Mock<IConfigurationSection>();
            projectEndpointSection.Setup(s => s.Value).Returns("https://test.endpoint.com");
            mockConfiguration.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(projectEndpointSection.Object);
            
            // Set up agent instructions
            var agentInstructionsSection = new Mock<IConfigurationSection>();
            agentInstructionsSection.Setup(s => s.Value).Returns("You are an expert Azure SDK developer and TypeSpec author. Your primary goal is to resolve all AZC analyzer and TypeSpec compilation errors in the TypeSpec files and produce a valid, compilable result that strictly follows Azure SDK and TypeSpec guidelines.\n\n### SYSTEM INSTRUCTIONS\n- All files (e.g., main.tsp, client.tsp) are available via FileSearchTool. Retrieve any file content by filename as needed.\n- Never modify main.tsp—only client.tsp may be changed.");
            mockConfiguration.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(agentInstructionsSection.Object);
            
            return new AppSettings(mockConfiguration.Object, mockLogger.Object);
        }

        private Fix CreateValidFixFromAnalyzer()
        {
            var mockErrors = new List<RuleError>
            {
                new RuleError("AZC0012", "Type name 'Client' is too generic")
            };
            
            var analyzer = new BuildErrorAnalyzer(new Mock<ILogger<BuildErrorAnalyzer>>().Object);
            return analyzer.GetFixes(mockErrors).First();
        }

        private AgentPromptFix CreateValidAgentPromptFix()
        {
            return new AgentPromptFix(
                "Fix the generic type name by replacing 'Client' with a more specific name like 'ServiceClient'",
                "Test context for agent prompt fix"
            );
        }

        private AgentPromptFix CreateAgentPromptFixWithoutContext()
        {
            return new AgentPromptFix(
                "Fix the generic type name by replacing 'Client' with a more specific name",
                null
            );
        }

        private List<Fix> CreateMultipleValidFixes()
        {
            var mockErrors = new List<RuleError>
            {
                new RuleError("AZC0012", "Type name 'Client' is too generic"),
                new RuleError("AZC0015", "Methods should follow naming convention"),
                new RuleError("AZC0018", "Missing required parameters"),
                new RuleError("AZC0020", "Return type not compatible"),
                new RuleError("AZC0025", "Missing documentation")
            };
            
            var analyzer = new BuildErrorAnalyzer(new Mock<ILogger<BuildErrorAnalyzer>>().Object);
            var analyzerFixes = analyzer.GetFixes(mockErrors).ToList();
            
            var agentPromptFixes = new List<Fix>
            {
                new AgentPromptFix("Fix prompt 1", "Context 1"),
                new AgentPromptFix("Fix prompt 2", "Context 2"),
                new AgentPromptFix("Fix prompt 3", null),
                new AgentPromptFix("Fix prompt 4", "Context 4"),
                new AgentPromptFix("Fix prompt 5", "Context 5")
            };
            
            return analyzerFixes.Concat(agentPromptFixes).ToList();
        }

        #endregion
    }
}