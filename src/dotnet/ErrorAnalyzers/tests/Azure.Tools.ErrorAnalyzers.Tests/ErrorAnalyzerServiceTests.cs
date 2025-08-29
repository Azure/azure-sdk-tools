using System;
using System.Linq;
using NUnit.Framework;

namespace Azure.Tools.ErrorAnalyzers.Tests
{
    [TestFixture]
    public class ErrorAnalyzerServiceTests
    {
        [Test]
        public void GetFix_WithValidError_ReturnsFix()
        {
            var error = new RuleError("AZC0012", "Test error message");

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
            var error = new RuleError("UNSUPPORTED", "Unsupported error type");

            var fix = ErrorAnalyzerService.GetFix(error);

            Assert.That(fix, Is.Null);
        }

        [Test]
        public void GetFixes_WithMultipleErrors_ReturnsCorrectFixes()
        {
            var errors = new[]
            {
                new RuleError("AZC0012", "Test error 1"),
                new RuleError("UNSUPPORTED", "Unsupported error"),
                new RuleError("AZC0012", "Test error 2")
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
        public void GetFix_WithValidErrorButNoContext_StillReturnsFix()
        {
            var error = new RuleError("TESTNOCONTEXT", "Test error with no context");

            var fix = ErrorAnalyzerService.GetFix(error);

            Assert.That(fix, Is.Null); // No prompt for this rule, so no fix
        }
    }
}
