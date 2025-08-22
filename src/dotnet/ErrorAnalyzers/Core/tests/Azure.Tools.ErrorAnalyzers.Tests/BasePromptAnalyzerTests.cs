// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using NUnit.Framework;

namespace Azure.Tools.ErrorAnalyzers.Tests
{
    [TestFixture]
    public class BasePromptAnalyzerTests
    {
        private class TestPromptAnalyzer : BasePromptAnalyzer
        {
            public TestPromptAnalyzer(string ruleId) : base(ruleId) { }
        }

        [Test]
        public void Constructor_WithNullRuleId_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TestPromptAnalyzer(null!));
        }

        [Test]
        public void CanFix_WithNullError_ReturnsFalse()
        {
            // Arrange
            var analyzer = new TestPromptAnalyzer("TEST001");

            // Act
            bool result = analyzer.CanFix(null!);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void CanFix_WithNullErrorType_ReturnsFalse()
        {
            // Arrange
            var analyzer = new TestPromptAnalyzer("TEST001");
            var error = new RuleError("TEST001", "test message") { type = null! };

            // Act
            bool result = analyzer.CanFix(error);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void CanFix_WithMatchingRuleId_ReturnsTrue()
        {
            // Arrange
            const string ruleId = "TEST001";
            var analyzer = new TestPromptAnalyzer(ruleId);
            var error = new RuleError(ruleId, "test message");

            // Act
            bool result = analyzer.CanFix(error);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void CanFix_WithDifferentCaseRuleId_ReturnsTrue()
        {
            // Arrange
            var analyzer = new TestPromptAnalyzer("TEST001");
            var error = new RuleError("test001", "test message");

            // Act
            bool result = analyzer.CanFix(error);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void CanFix_WithNonMatchingRuleId_ReturnsFalse()
        {
            // Arrange
            var analyzer = new TestPromptAnalyzer("TEST001");
            var error = new RuleError("TEST002", "test message");

            // Act
            bool result = analyzer.CanFix(error);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void GetFix_WithNullError_ReturnsNull()
        {
            // Arrange
            var analyzer = new TestPromptAnalyzer("TEST001");

            // Act
            Fix? result = analyzer.GetFix(null!);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetFix_WithNullMessage_ReturnsNull()
        {
            // Arrange
            var analyzer = new TestPromptAnalyzer("TEST001");
            var error = new RuleError("TEST001", "test") { message = null! };

            // Act
            Fix? result = analyzer.GetFix(error);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetFix_WithNonMatchingRuleId_ReturnsNull()
        {
            // Arrange
            var analyzer = new TestPromptAnalyzer("TEST001");
            var error = new RuleError("TEST002", "Test message");

            // Act
            Fix? result = analyzer.GetFix(error);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetFix_WithValidErrorButNoPrompt_ReturnsNull()
        {
            // Arrange
            var analyzer = new TestPromptAnalyzer("UNKNOWN_RULE");
            var error = new RuleError("UNKNOWN_RULE", "Test message");

            // Act
            Fix? result = analyzer.GetFix(error);

            // Assert
            Assert.That(result, Is.Null);
        }
    }
}
