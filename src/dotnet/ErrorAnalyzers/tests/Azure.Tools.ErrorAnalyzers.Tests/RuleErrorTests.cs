using System;
using NUnit.Framework;

namespace Azure.Tools.ErrorAnalyzers.Tests
{
    [TestFixture]
    public class RuleErrorTests
    {
        [Test]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            const string errorType = "AZC0030";
            const string errorMessage = "Model name ends with invalid suffix";

            var ruleError = new RuleError(errorType, errorMessage);

            Assert.That(ruleError.type, Is.EqualTo(errorType));
            Assert.That(ruleError.message, Is.EqualTo(errorMessage));
        }

        [Test]
        public void Constructor_WithNullType_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentNullException>(() => new RuleError(null!, "Valid message"));
        }

        [Test]
        public void Constructor_WithEmptyType_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new RuleError(string.Empty, "Valid message"));
        }

        [Test]
        public void Constructor_WithWhitespaceType_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new RuleError("   ", "Valid message"));
        }

        [Test]
        public void Constructor_WithNullMessage_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentNullException>(() => new RuleError("AZC0030", null!));
        }

        [Test]
        public void Constructor_WithEmptyMessage_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new RuleError("AZC0030", string.Empty));
        }

        [Test]
        public void Constructor_WithWhitespaceMessage_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new RuleError("AZC0030", "   "));
        }

        [Test]
        public void Properties_AreInitOnly_CannotBeSetAfterConstruction()
        {
            var ruleError = new RuleError("AZC0030", "Test message");

            Assert.That(ruleError.type, Is.EqualTo("AZC0030"));
            Assert.That(ruleError.message, Is.EqualTo("Test message"));
        }

        [Test]
        public void RuleError_WithLongMessage_HandlesCorrectly()
        {
            const string longMessage = "This is a very long error message that might be encountered in real-world scenarios where the analyzer provides detailed information about what went wrong and how to fix it. This tests that our validation and storage can handle longer strings without issues.";

            var ruleError = new RuleError("AZC0030", longMessage);

            Assert.That(ruleError.message, Is.EqualTo(longMessage));
            Assert.That(ruleError.message.Length, Is.GreaterThan(100));
        }

        [Test]
        public void RuleError_WithSpecialCharacters_HandlesCorrectly()
        {
            const string specialMessage = "Error with special chars: <>&\"'`~!@#$%^&*()+={}[]|\\:;\"'<>?,./";
            const string specialType = "AZC-0012_Test.Type";

            var ruleError = new RuleError(specialType, specialMessage);

            Assert.That(ruleError.type, Is.EqualTo(specialType));
            Assert.That(ruleError.message, Is.EqualTo(specialMessage));
        }
    }
}
