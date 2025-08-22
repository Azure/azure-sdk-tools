// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using NUnit.Framework;

namespace Azure.Tools.ErrorAnalyzers.Tests
{
    [TestFixture]
    public class AnalyzerPromptsTests
    {
        [Test]
        public void TryGetPrompt_WithNullRuleId_ReturnsFalse()
        {
            // Arrange
            string? ruleId = null;

            // Act
            bool result = AnalyzerPrompts.TryGetPrompt(ruleId!, out string prompt);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(prompt, Is.EqualTo(string.Empty));
        }

        [Test]
        public void TryGetPrompt_WithEmptyRuleId_ReturnsFalse()
        {
            // Arrange
            string ruleId = string.Empty;

            // Act
            bool result = AnalyzerPrompts.TryGetPrompt(ruleId, out string prompt);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(prompt, Is.EqualTo(string.Empty));
        }

        [Test]
        public void TryGetPrompt_WithWhitespaceRuleId_ReturnsFalse()
        {
            // Arrange
            string ruleId = "   ";

            // Act
            bool result = AnalyzerPrompts.TryGetPrompt(ruleId, out string prompt);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(prompt, Is.EqualTo(string.Empty));
        }

        [Test]
        public void TryGetPrompt_WithUnknownRuleId_ReturnsFalse()
        {
            // Arrange
            string ruleId = "UNKNOWN_RULE";

            // Act
            bool result = AnalyzerPrompts.TryGetPrompt(ruleId, out string prompt);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(prompt, Is.EqualTo(string.Empty));
        }

        [Test]
        public void TryGetContext_WithNullParameters_ReturnsFalse()
        {
            // Arrange
            string? ruleId = null;
            string? errorMessage = null;

            // Act
            bool result = AnalyzerPrompts.TryGetContext(ruleId!, errorMessage!, out string? context);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(context, Is.Null);
        }

        [Test]
        public void TryGetContext_WithEmptyParameters_ReturnsFalse()
        {
            // Arrange
            string ruleId = string.Empty;
            string errorMessage = string.Empty;

            // Act
            bool result = AnalyzerPrompts.TryGetContext(ruleId, errorMessage, out string? context);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(context, Is.Null);
        }

        [Test]
        public void TryGetContext_WithUnknownRuleId_ReturnsFalse()
        {
            // Arrange
            string ruleId = "UNKNOWN_RULE";
            string errorMessage = "Test error message";

            // Act
            bool result = AnalyzerPrompts.TryGetContext(ruleId, errorMessage, out string? context);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(context, Is.Null);
        }
    }
}
