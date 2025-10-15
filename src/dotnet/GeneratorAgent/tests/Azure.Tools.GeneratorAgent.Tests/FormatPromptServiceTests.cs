using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    internal class FormatPromptServiceTests
    {
        #region Constructor Tests

        [Test]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            var logger = NullLogger<FormatPromptService>.Instance;
            var appSettings = CreateTestAppSettings();

            var service = new FormatPromptService(appSettings);

            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new FormatPromptService(null!));
            Assert.That(ex?.ParamName, Is.EqualTo("appSettings"));
        }

        #endregion

        #region ConvertFixesToBatchPrompt Tests

        [Test]
        public void ConvertFixesToBatchPrompt_WithNullFixes_ShouldThrowArgumentNullException()
        {
            var service = CreateFormatPromptService();

            var ex = Assert.Throws<ArgumentNullException>(() => service.ConvertFixesToBatchPrompt(null!));
            Assert.That(ex?.ParamName, Is.EqualTo("fixes"));
        }

        [Test]
        public void ConvertFixesToBatchPrompt_WithEmptyFixes_ShouldThrowArgumentException()
        {
            var service = CreateFormatPromptService();
            var emptyFixes = new List<Fix>();

            var ex = Assert.Throws<ArgumentException>(() => service.ConvertFixesToBatchPrompt(emptyFixes));
            Assert.That(ex?.ParamName, Is.EqualTo("fixes"));
        }

        [Test]
        public void ConvertFixesToBatchPrompt_WithSingleAgentPromptFix_ShouldReturnValidPrompt()
        {
            var service = CreateFormatPromptService();
            var fixes = new List<Fix> { CreateValidAgentPromptFix() };

            var result = service.ConvertFixesToBatchPrompt(fixes);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.Not.Empty);
                Assert.That(result, Does.Contain("=== ERRORS TO FIX ==="));
                Assert.That(result, Does.Contain("=== HOW TO FIX ==="));
                Assert.That(result, Does.Contain("### SYSTEM INSTRUCTIONS"));
                Assert.That(result, Does.Contain("GENERIC:"));
                Assert.That(result, Does.Contain("Fix the generic type name"));
                Assert.That(result, Does.Contain("Error requiring analysis"));
            });
        }

        [Test]
        public void ConvertFixesToBatchPrompt_WithMultipleAgentPromptFixes_ShouldCombinePrompts()
        {
            var service = CreateFormatPromptService();
            var fixes = new List<Fix>
            {
                new AgentPromptFix("First fix prompt", "First context"),
                new AgentPromptFix("Second fix prompt", "Second context"),
                new AgentPromptFix("Third fix prompt", null)
            };

            var result = service.ConvertFixesToBatchPrompt(fixes);

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("=== ERRORS TO FIX ==="));
                Assert.That(result, Does.Contain("=== HOW TO FIX ==="));
                Assert.That(result, Does.Contain("GENERIC (2 instances):"));
                Assert.That(result, Does.Contain("UNKNOWN:"));
                Assert.That(result, Does.Contain("GENERIC: First fix prompt"));
                Assert.That(result, Does.Contain("UNKNOWN: Third fix prompt"));
                Assert.That(result, Does.Contain("Error requiring analysis"));
                Assert.That(result, Does.Contain("Unknown error"));
            });
        }

        [Test]
        public void ConvertFixesToBatchPrompt_WithGenericFixes_ShouldIncludeFixTypeAndAction()
        {
            var service = CreateFormatPromptService();
            var fixes = new List<Fix> { CreateGenericFix() };

            var result = service.ConvertFixesToBatchPrompt(fixes);

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("GENERIC: Generic fix prompt"));
                Assert.That(result, Does.Contain("=== ERRORS TO FIX ==="));
                Assert.That(result, Does.Contain("Error requiring analysis"));
            });
        }

        [Test]
        public void ConvertFixesToBatchPrompt_WithMixedFixTypes_ShouldHandleBothTypes()
        {
            var service = CreateFormatPromptService();
            var fixes = new List<Fix>
            {
                CreateValidAgentPromptFix(),
                CreateGenericFix()
            };

            var result = service.ConvertFixesToBatchPrompt(fixes);

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("=== ERRORS TO FIX ==="));
                Assert.That(result, Does.Contain("=== HOW TO FIX ==="));
                Assert.That(result, Does.Contain("GENERIC (2 instances):"));
                Assert.That(result, Does.Contain("Fix the generic type name"));
                Assert.That(result, Does.Contain("Error requiring analysis"));
            });
        }

        #endregion

        #region Performance Tests

        [Test]
        public void ConvertFixesToBatchPrompt_WithMultipleFixes_ShouldCompleteInReasonableTime()
        {
            var service = CreateFormatPromptService();
            var fixes = CreateMultipleValidFixes();

            var startTime = DateTime.UtcNow;
            var result = service.ConvertFixesToBatchPrompt(fixes);
            var endTime = DateTime.UtcNow;

            var duration = endTime - startTime;
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(duration.TotalMilliseconds, Is.LessThan(1000), "Should complete batch processing in under 1 second");
            });
        }

        #endregion

        #region Helper Methods

        private FormatPromptService CreateFormatPromptService()
        {
            var appSettings = CreateTestAppSettings();
            return new FormatPromptService(appSettings);
        }

        private AppSettings CreateTestAppSettings()
        {
            var mockConfiguration = new Mock<IConfiguration>();
            var logger = NullLogger<AppSettings>.Instance;
            
            // Set up required configuration values
            var projectEndpointSection = new Mock<IConfigurationSection>();
            projectEndpointSection.Setup(s => s.Value).Returns("https://test.endpoint.com");
            mockConfiguration.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(projectEndpointSection.Object);
            
            // Set up agent instructions
            var agentInstructionsSection = new Mock<IConfigurationSection>();
            agentInstructionsSection.Setup(s => s.Value).Returns("You are an expert Azure SDK developer and TypeSpec author. Your primary goal is to resolve all AZC analyzer and TypeSpec compilation errors in the TypeSpec files and produce a valid, compilable result that strictly follows Azure SDK and TypeSpec guidelines.\n\n### SYSTEM INSTRUCTIONS\n- All files (e.g., main.tsp, client.tsp) are available via FileSearchTool. Retrieve any file content by filename as needed.\n- Never modify main.tsp—only client.tsp may be changed.");
            mockConfiguration.Setup(c => c.GetSection("AzureSettings:AgentInstructions")).Returns(agentInstructionsSection.Object);
            
            // Set up fix prompt template
            var fixPromptTemplateSection = new Mock<IConfigurationSection>();
            fixPromptTemplateSection.Setup(s => s.Value).Returns("\n\n### SPECIFIC FIX TO APPLY\n{0}\n\n### CONTEXT\n{1}\n\n### RESPONSE FORMAT\nYou MUST respond with ONLY a JSON object in the following exact format:\n{{\n    \"path\": \"client.tsp\",\n    \"content\": \"... the complete updated client.tsp file content here ...\"\n}}\n\nThe \"content\" field must contain the complete, corrected client.tsp file content with all the fixes applied.\nDo not include any explanations, markdown, or other text outside of this JSON structure.\n\nNow apply this fix following the system instructions above.");
            mockConfiguration.Setup(c => c.GetSection("AzureSettings:FixPromptTemplate")).Returns(fixPromptTemplateSection.Object);
            
            return new AppSettings(mockConfiguration.Object, logger);
        }

        private AgentPromptFix CreateValidAgentPromptFix()
        {
            return new AgentPromptFix(
                "Fix the generic type name by replacing 'Client' with a more specific name like 'ServiceClient'",
                "Test context for agent prompt fix"
            );
        }

        private Fix CreateGenericFix()
        {
            return new AgentPromptFix("Generic fix prompt", "Generic context");
        }

        private List<Fix> CreateMultipleValidFixes()
        {
            var fixes = new List<Fix>();
            for (int i = 1; i <= 10; i++)
            {
                fixes.Add(new AgentPromptFix($"Fix prompt {i}", $"Context {i}"));
            }
            return fixes;
        }

        #endregion
    }
}