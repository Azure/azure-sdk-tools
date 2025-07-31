using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Azure.Tools.ErrorAnalyzers.Tests
{
    [TestFixture]
    public class ErrorAnalyzerServiceTests
    {
        private TestAnalyzerProvider? testProvider;

        [SetUp]
        public void Setup()
        {
            testProvider = new TestAnalyzerProvider();
        }

        [Test]
        public void GetFix_WithValidError_ReturnsFix()
        {
            ErrorAnalyzerService.RegisterProvider(testProvider!);
            var error = new RuleError("TEST001", "Test error message");

            var fix = ErrorAnalyzerService.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix!.Action, Is.EqualTo(FixAction.AgentPrompt));
        }

        [Test]
        public void GetFix_WithNullError_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ErrorAnalyzerService.GetFix(null!));
        }

        [Test]
        public void GetFix_WithUnsupportedError_ReturnsNull()
        {
            ErrorAnalyzerService.RegisterProvider(testProvider!);
            var error = new RuleError("UNSUPPORTED", "Unsupported error type");

            var fix = ErrorAnalyzerService.GetFix(error);

            Assert.That(fix, Is.Null);
        }

        [Test]
        public void GetFixes_WithMultipleErrors_ReturnsCorrectFixes()
        {
            ErrorAnalyzerService.RegisterProvider(testProvider!);
            var errors = new[]
            {
                new RuleError("TEST001", "Test error 1"),
                new RuleError("UNSUPPORTED", "Unsupported error"),
                new RuleError("TEST001", "Test error 2")
            };

            var fixes = ErrorAnalyzerService.GetFixes(errors).ToList();

            Assert.That(fixes.Count, Is.EqualTo(2));
            Assert.That(fixes.All(f => f.Action == FixAction.AgentPrompt), Is.True);
        }

        [Test]
        public void GetFixes_WithNullErrors_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ErrorAnalyzerService.GetFixes(null!).ToList());
        }

        [Test]
        public void RegisterProvider_WithNullProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ErrorAnalyzerService.RegisterProvider(null!));
        }
    }

    internal class TestAnalyzer : AgentRuleAnalyzer
    {
        public override bool CanFix(RuleError error)
        {
            return error.type == "TEST001";
        }

        public override Fix? GetFix(RuleError error)
        {
            if (!CanFix(error))
            {
                return null;
            }

            return new AgentPromptFix("Test prompt for fixing the error", "Test context");
        }
    }

    internal class TestAnalyzerProvider : IAnalyzerProvider
    {
        public IEnumerable<AgentRuleAnalyzer> GetAnalyzers()
        {
            return new[] { new TestAnalyzer() };
        }
    }

}
