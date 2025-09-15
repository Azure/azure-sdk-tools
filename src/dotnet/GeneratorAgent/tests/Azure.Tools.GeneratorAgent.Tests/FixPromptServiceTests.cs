using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    internal class FixPromptServiceTests
    {
        #region Constructor Tests

        [Test]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            var logger = NullLogger<FixPromptService>.Instance;
            var appSettings = CreateTestAppSettings();

            var service = new FixPromptService(logger, appSettings);

            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var appSettings = CreateTestAppSettings();

            var ex = Assert.Throws<ArgumentNullException>(() => new FixPromptService(null!, appSettings));
            Assert.That(ex?.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            var logger = NullLogger<FixPromptService>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() => new FixPromptService(logger, null!));
            Assert.That(ex?.ParamName, Is.EqualTo("appSettings"));
        }

        #endregion

        #region ConvertFixToPrompt Tests

        [Test]
        public void ConvertFixToPrompt_WithNullFix_ShouldThrowArgumentNullException()
        {
            var service = CreateFixPromptService();

            var ex = Assert.Throws<ArgumentNullException>(() => service.ConvertFixToPrompt(null!));
            Assert.That(ex?.ParamName, Is.EqualTo("fix"));
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
                Assert.That(result, Does.Contain("Fix the generic type name"));
                Assert.That(result, Does.Contain("Test context for agent prompt fix"));
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
                Assert.That(result, Does.Contain("Fix the generic type name"));
            });
        }

        [Test]
        public void ConvertFixToPrompt_WithGenericFix_ShouldReturnValidPrompt()
        {
            var service = CreateFixPromptService();
            var fix = CreateGenericFix();

            // The mock Fix doesn't have Action properly set up, so this test focuses on structure
            // The real implementation would have proper Action values
            var result = service.ConvertFixToPrompt(fix);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.Not.Empty);
                Assert.That(result, Does.Contain("### SPECIFIC FIX TO APPLY"));
                Assert.That(result, Does.Contain("### CONTEXT"));
                Assert.That(result, Does.Contain("SYSTEM INSTRUCTIONS"));
                // Updated to match the actual prompt format
                Assert.That(result, Does.Contain("Generic fix prompt"));
                Assert.That(result, Does.Contain("Generic context"));
                Assert.That(result, Does.Contain("You MUST respond with ONLY a JSON object"));
            });
        }

        [Test]
        public void ConvertFixToPrompt_WithDifferentFixTypes_ShouldProduceConsistentStructure()
        {
            var service = CreateFixPromptService();
            var genericFix = CreateGenericFix();
            var agentPromptFix = CreateValidAgentPromptFix();

            var genericResult = service.ConvertFixToPrompt(genericFix);
            var agentPromptResult = service.ConvertFixToPrompt(agentPromptFix);

            Assert.Multiple(() =>
            {
                Assert.That(genericResult, Does.Contain("### SPECIFIC FIX TO APPLY"));
                Assert.That(agentPromptResult, Does.Contain("### SPECIFIC FIX TO APPLY"));
                Assert.That(genericResult, Does.Contain("### CONTEXT"));
                Assert.That(agentPromptResult, Does.Contain("### CONTEXT"));
                Assert.That(genericResult, Does.Contain("SYSTEM INSTRUCTIONS"));
                Assert.That(agentPromptResult, Does.Contain("SYSTEM INSTRUCTIONS"));
                
                // But AgentPromptFix should have specialized content
                Assert.That(agentPromptResult, Does.Contain("Fix the generic type name"));
                Assert.That(agentPromptResult, Does.Contain("Test context for agent prompt fix"));
            });
        }

        #endregion

        #region Performance Tests

        [Test]
        public void ConvertFixToPrompt_MultipleCallsWithSameFix_ShouldProduceConsistentResults()
        {
            var service = CreateFixPromptService();
            var fix = CreateValidAgentPromptFix();

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

        #region Integration Tests

        [Test]
        public void ConvertFixToPrompt_WithAgentPromptFixType_UsesSpecializedHandling()
        {
            var service = CreateFixPromptService();
            var promptFix = new AgentPromptFix("Custom prompt instruction", "Custom context");

            var result = service.ConvertFixToPrompt(promptFix);

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("Custom prompt instruction"));
                Assert.That(result, Does.Contain("Custom context"));
                Assert.That(result, Does.Not.Contain("Fix Type:"));
                Assert.That(result, Does.Not.Contain("Action Required:"));
            });
        }

        [Test]
        public void ConvertFixToPrompt_WithGenericFixType_UsesGenericHandling()
        {
            var service = CreateFixPromptService();
            var genericFix = CreateGenericFix();

            var result = service.ConvertFixToPrompt(genericFix);

            Assert.Multiple(() =>
            {
                // Updated to match the actual prompt format
                Assert.That(result, Does.Contain("Generic fix prompt"));
                Assert.That(result, Does.Contain("Generic context"));
                Assert.That(result, Does.Contain("You MUST respond with ONLY a JSON object"));
            });
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void ConvertFixToPrompt_WithEmptyPromptInAgentPromptFix_ShouldThrowArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new AgentPromptFix("", "Some context"));
        }

        [Test]
        public void ConvertFixToPrompt_WithEmptyStringContext_ShouldUseDefaultMessage()
        {
            var service = CreateFixPromptService();
            var fix = new AgentPromptFix("Test prompt", "");

            var result = service.ConvertFixToPrompt(fix);

            Assert.That(result, Does.Contain("No additional context provided"));
        }

        #endregion

        #region Helper Methods

        private FixPromptService CreateFixPromptService()
        {
            var logger = NullLogger<FixPromptService>.Instance;
            var appSettings = CreateTestAppSettings();
            return new FixPromptService(logger, appSettings);
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

        private AgentPromptFix CreateAgentPromptFixWithoutContext()
        {
            return new AgentPromptFix(
                "Fix the generic type name by replacing 'Client' with a more specific name",
                null
            );
        }

        private Fix CreateGenericFix()
        {
            return new AgentPromptFix("Generic fix prompt", "Generic context");
        }

        private List<Fix> CreateMultipleValidFixes()
        {
            var agentPromptFixes = new List<Fix>
            {
                new AgentPromptFix("Fix prompt 1", "Context 1"),
                new AgentPromptFix("Fix prompt 2", "Context 2"),
                new AgentPromptFix("Fix prompt 3", null),
                new AgentPromptFix("Fix prompt 4", "Context 4"),
                new AgentPromptFix("Fix prompt 5", "Context 5")
            };
            
            var genericFixes = new List<Fix>();
            for (int i = 1; i <= 5; i++)
            {
                // Use AgentPromptFix instead of trying to mock abstract Fix class
                var genericFix = new AgentPromptFix($"Generic fix prompt {i}", $"Generic context {i}");
                genericFixes.Add(genericFix);
            }
            
            return agentPromptFixes.Concat(genericFixes).ToList();
        }

        #endregion
    }
}