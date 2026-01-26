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
        public void GetPromptFix_WithNullRuleId_ReturnsFallback()
        {
            // Arrange
            string? ruleId = null;

            // Act
            var fix = AnalyzerPrompts.GetPromptFix(ruleId!);

            // Assert
            Assert.That(fix, Is.Not.Null);
            Assert.That(fix.Prompt, Does.Contain("Analyze and Fix Unknown Error"));
        }

        [Test]
        public void GetPromptFix_WithEmptyRuleId_ReturnsFallback()
        {
            // Arrange
            string ruleId = string.Empty;

            // Act
            var fix = AnalyzerPrompts.GetPromptFix(ruleId);

            // Assert
            Assert.That(fix, Is.Not.Null);
            Assert.That(fix.Prompt, Does.Contain("Analyze and Fix Unknown Error"));
        }

        [Test]
        public void GetPromptFix_WithWhitespaceRuleId_ReturnsFallback()
        {
            // Arrange
            string ruleId = "   ";

            // Act
            var fix = AnalyzerPrompts.GetPromptFix(ruleId);

            // Assert
            Assert.That(fix, Is.Not.Null);
            Assert.That(fix.Prompt, Does.Contain("Analyze and Fix Unknown Error"));
        }

        [Test]
        public void GetPromptFix_WithUnknownRuleId_ReturnsFallback()
        {
            // Arrange
            string ruleId = "UNKNOWN_RULE";

            // Act
            var fix = AnalyzerPrompts.GetPromptFix(ruleId);

            // Assert
            Assert.That(fix, Is.Not.Null);
            Assert.That(fix.Prompt, Does.Contain("Analyze and Fix Unknown Error"));
        }

        [Test]
        public void GetPromptFix_WithValidRuleId_ReturnsSpecificFix()
        {
            // Arrange
            string ruleId = "AZC0030"; // This should exist in the prompts

            // Act
            var fix = AnalyzerPrompts.GetPromptFix(ruleId);

            // Assert
            Assert.That(fix, Is.Not.Null);
            Assert.That(fix.Prompt, Is.Not.Empty);
            Assert.That(fix.Prompt, Does.Not.Contain("Analyze and Fix Unknown Error")); // Should not be fallback
        }

        [Test]
        public void GetPromptFix_WithValidRuleId_ReturnsCorrectData()
        {
            // Arrange
            string ruleId = "AZC0030";

            // Act
            var fix = AnalyzerPrompts.GetPromptFix(ruleId);

            // Assert
            Assert.That(fix, Is.Not.Null);
            Assert.That(fix.Prompt, Does.Contain("AZC0030"));
            Assert.That(fix.Context, Is.Not.Empty);
        }

        [Test]
        public void GetAllRuleIds_ReturnsNonEmptyCollection()
        {
            // Act
            var ruleIds = AnalyzerPrompts.GetAllRuleIds();

            // Assert
            Assert.That(ruleIds, Is.Not.Empty);
            Assert.That(ruleIds, Does.Contain("AZC0030"));
        }
    }
}
