// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using NUnit.Framework;
using Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.Tests.ClientRuleAnalyzers
{
    [TestFixture]
    internal class BasePromptAnalyzerTests
    {
        [Test]
        public void Constructor_WhenCalled_InitializesRuleType()
        {
            // Arrange & Act
            var analyzer = new BasePromptAnalyzer(AzcRuleType.AZC0012);

            // Assert
            Assert.That(analyzer.RuleType, Is.EqualTo(AzcRuleType.AZC0012));
        }

        [Test]
        [TestCase(AzcRuleType.AZC0012, "AZC0012", true, Description = "Exact match should return true")]
        [TestCase(AzcRuleType.AZC0012, "azc0012", true, Description = "Case insensitive match should return true")]
        [TestCase(AzcRuleType.AZC0012, "AZC0030", false, Description = "Different rule should return false")]
        [TestCase(AzcRuleType.AZC0030, "AZC0030", true, Description = "Different rule type exact match should return true")]
        public void CanFix_WhenCalledWithDifferentRules_ReturnsExpectedResult(AzcRuleType ruleType, string errorType, bool expectedResult)
        {
            // Arrange
            var analyzer = new BasePromptAnalyzer(ruleType);
            var error = new RuleError(errorType, "Test message");

            // Act
            bool result = analyzer.CanFix(error);

            // Assert
            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [Test]
        public void CanFix_WhenErrorIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var analyzer = new BasePromptAnalyzer(AzcRuleType.AZC0012);

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => analyzer.CanFix(null));
            Assert.That(ex.ParamName, Is.Not.Null);
        }

        [Test]
        public void CanFix_WhenErrorTypeIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var analyzer = new BasePromptAnalyzer(AzcRuleType.AZC0012);

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new RuleError(null!, "message"));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex!.ParamName, Is.EqualTo("type"));
        }

        [Test]
        public void GetFix_WhenErrorIsValid_ReturnsFixWithPromptAndContext()
        {
            // Arrange
            var analyzer = new BasePromptAnalyzer(AzcRuleType.AZC0012);
            var error = new RuleError("AZC0012", "Test error message");

            // Act
            var fix = analyzer.GetFix(error);

            // Assert
            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.TypeOf<AgentPromptFix>());
        }

        [Test]
        public void GetFix_WhenErrorIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var analyzer = new BasePromptAnalyzer(AzcRuleType.AZC0012);

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => analyzer.GetFix(null!));
            Assert.That(ex, Is.Not.Null);
        }

        [Test]
        public void GetFix_WhenErrorMessageIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var analyzer = new BasePromptAnalyzer(AzcRuleType.AZC0012);

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new RuleError("AZC0012", null!));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex!.ParamName, Is.EqualTo("message"));
        }

        [Test]
        public void GetFix_WhenRuleTypeDoesNotMatch_ReturnsNull()
        {
            // Arrange
            var analyzer = new BasePromptAnalyzer(AzcRuleType.AZC0012);
            var error = new RuleError("AZC0030", "Test error message");

            // Act
            var fix = analyzer.GetFix(error);

            // Assert
            Assert.That(fix, Is.Null);
        }

        [Test]
        public void ToString_WhenCalled_ReturnsExpectedFormat()
        {
            // Arrange
            var analyzer = new BasePromptAnalyzer(AzcRuleType.AZC0012);
            var expected = "Rule Analyzer: AZC0012";

            // Act
            string result = analyzer.ToString();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
