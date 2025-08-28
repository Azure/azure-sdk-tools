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
        public void TryGetPromptFix_WithNullRuleId_ReturnsFalse()
        {
            // Arrange
            string? ruleId = null;

            // Act
            bool result = AnalyzerPrompts.TryGetPromptFix(ruleId!, out AgentPromptFix? fix);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(fix, Is.Null);
        }

        [Test]
        public void TryGetPromptFix_WithEmptyRuleId_ReturnsFalse()
        {
            // Arrange
            string ruleId = string.Empty;

            // Act
            bool result = AnalyzerPrompts.TryGetPromptFix(ruleId, out AgentPromptFix? fix);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(fix, Is.Null);
        }

        [Test]
        public void TryGetPromptFix_WithWhitespaceRuleId_ReturnsFalse()
        {
            // Arrange
            string ruleId = "   ";

            // Act
            bool result = AnalyzerPrompts.TryGetPromptFix(ruleId, out AgentPromptFix? fix);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(fix, Is.Null);
        }

        [Test]
        public void TryGetPromptFix_WithUnknownRuleId_ReturnsFalse()
        {
            // Arrange
            string ruleId = "UNKNOWN_RULE";

            // Act
            bool result = AnalyzerPrompts.TryGetPromptFix(ruleId, out AgentPromptFix? fix);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(fix, Is.Null);
        }

        [Test]
        public void TryGetPromptFix_WithValidRuleId_ReturnsTrue()
        {
            // Arrange
            string ruleId = "AZC0012"; // This should exist in the prompts

            // Act
            bool result = AnalyzerPrompts.TryGetPromptFix(ruleId, out AgentPromptFix? fix);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(fix, Is.Not.Null);
            Assert.That(fix!.Prompt, Is.Not.Empty);
        }

        [Test]
        public void TryGetPromptFix_WithValidRuleId_ReturnsCorrectData()
        {
            // Arrange
            string ruleId = "AZC0012";

            // Act
            bool result = AnalyzerPrompts.TryGetPromptFix(ruleId, out AgentPromptFix? fix);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(fix, Is.Not.Null);
            Assert.That(fix!.Prompt, Does.Contain("Fix AZC0012"));
            Assert.That(fix.Context, Is.Not.Empty);
        }

        [Test]
        public void GetAllRuleIds_ReturnsNonEmptyCollection()
        {
            // Act
            var ruleIds = AnalyzerPrompts.GetAllRuleIds();

            // Assert
            Assert.That(ruleIds, Is.Not.Empty);
            Assert.That(ruleIds, Does.Contain("AZC0012"));
        }
    }
}
