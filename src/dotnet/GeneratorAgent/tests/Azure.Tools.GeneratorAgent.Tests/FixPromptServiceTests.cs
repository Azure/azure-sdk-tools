using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent;
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

            var service = new FixPromptService(mockLogger.Object);

            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new FixPromptService(null!));
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
                Assert.That(result, Does.Contain("🔧 **APPLY FIX TO CURRENT CLIENT.TSP**"));
                Assert.That(result, Does.Contain("**Context:**"));
                Assert.That(result, Does.Contain("**Fix Instructions:**"));
                Assert.That(result, Does.Contain("**CRITICAL Requirements:**"));
                Assert.That(result, Does.Contain("Use the FileSearchTool to reference other TypeSpec files as needed"));
                Assert.That(result, Does.Contain("Ensure the fix resolves the AZC violation while maintaining compatibility"));
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
                Assert.That(result, Does.Contain("🔧 **APPLY FIX TO CURRENT CLIENT.TSP**"));
                Assert.That(result, Does.Contain("**Context:**"));
                Assert.That(result, Does.Contain("**Fix Instructions:**"));
                Assert.That(result, Does.Contain("**CRITICAL Requirements:**"));
                Assert.That(result, Does.Contain("Use the FileSearchTool to reference other TypeSpec files as needed"));
                Assert.That(result, Does.Contain("Ensure the fix resolves the AZC violation while maintaining compatibility"));
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
                Assert.That(result, Does.Contain("🔧 **APPLY FIX TO CURRENT CLIENT.TSP**"));
                Assert.That(result, Does.Not.Contain("**Context:**"));
                Assert.That(result, Does.Contain("**Fix Instructions:**"));
                Assert.That(result, Does.Contain("**CRITICAL Requirements:**"));
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
                Assert.That(analyzerResult, Does.Contain("🔧 **APPLY FIX TO CURRENT CLIENT.TSP**"));
                Assert.That(agentPromptResult, Does.Contain("🔧 **APPLY FIX TO CURRENT CLIENT.TSP**"));
                Assert.That(analyzerResult, Does.Contain("**CRITICAL Requirements:**"));
                Assert.That(agentPromptResult, Does.Contain("**CRITICAL Requirements:**"));
                Assert.That(analyzerResult, Does.Contain("```typespec"));
                Assert.That(agentPromptResult, Does.Contain("```typespec"));
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
            return new FixPromptService(mockLogger.Object);
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