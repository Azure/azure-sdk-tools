// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using NUnit.Framework;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers.Tests
{
    [TestFixture]
    public class AZC0035RuleAnalyzerTests
    {
        private const string TestErrorMessage = "Output model type 'UserInfo' should have a corresponding method in a model factory class. Add a static method that returns 'UserInfo' to a class ending with 'ModelFactory'.";

        [Test]
        public void CanFix_ValidAZC0035Error_ReturnsTrue()
        {
            AZC0035RuleAnalyzer analyzer = new AZC0035RuleAnalyzer();
            RuleError error = new RuleError("AZC0035", TestErrorMessage);

            bool result = analyzer.CanFix(error);

            Assert.That(result, Is.True);
        }

        [Test]
        public void CanFix_InvalidErrorType_ReturnsFalse()
        {
            AZC0035RuleAnalyzer analyzer = new AZC0035RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", "Some other error message");

            bool result = analyzer.CanFix(error);

            Assert.That(result, Is.False);
        }

        [Test]
        public void CanFix_NullError_ReturnsFalse()
        {
            AZC0035RuleAnalyzer analyzer = new AZC0035RuleAnalyzer();

            bool result = analyzer.CanFix(null!);

            Assert.That(result, Is.False);
        }

        [Test]
        public void GetFix_ReturnsAgentPromptFixFromProvider()
        {
            AZC0035RuleAnalyzer analyzer = new AZC0035RuleAnalyzer();
            RuleError error = new RuleError("AZC0035", TestErrorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<AgentPromptFix>());
            
            AgentPromptFix promptFix = (AgentPromptFix)fix!;
            Assert.That(promptFix.Action, Is.EqualTo(FixAction.AgentPrompt));
        }

        [Test]
        public void GetFix_UsesAnalyzerPromptProvider()
        {
            AZC0035RuleAnalyzer analyzer = new AZC0035RuleAnalyzer();
            RuleError error = new RuleError("AZC0035", TestErrorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<AgentPromptFix>());
            AgentPromptFix promptFix = (AgentPromptFix)fix!;
            
            // The specific content of prompt and context is now managed by AnalyzerPromptProvider
            Assert.That(promptFix.Context, Is.Not.Null);
            Assert.That(promptFix.Prompt, Is.Not.Null);
        }

        [Test]
        public void GetFix_InvalidErrorType_ReturnsNull()
        {
            AZC0035RuleAnalyzer analyzer = new AZC0035RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", TestErrorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Null);
        }

        [Test]
        public void GetFix_NullMessage_ThrowsArgumentException()
        {
            AZC0035RuleAnalyzer analyzer = new AZC0035RuleAnalyzer();

            Assert.Throws<ArgumentNullException>(() => new RuleError("AZC0035", null!));
        }
    }
}
