// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using NUnit.Framework;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers.Tests
{
    [TestFixture]
    public class AZC0034RuleAnalyzerTests
    {
        private const string TestErrorMessage = "Type name 'Response' conflicts with 'System.Net.Http.HttpResponseMessage'. Consider renaming to avoid confusion.";

        [Test]
        public void CanFix_ValidAZC0034Error_ReturnsTrue()
        {
            AZC0034RuleAnalyzer analyzer = new AZC0034RuleAnalyzer();
            RuleError error = new RuleError("AZC0034", TestErrorMessage);

            bool result = analyzer.CanFix(error);

            Assert.That(result, Is.True);
        }

        [Test]
        public void CanFix_InvalidErrorType_ReturnsFalse()
        {
            AZC0034RuleAnalyzer analyzer = new AZC0034RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", "Some other error message");

            bool result = analyzer.CanFix(error);

            Assert.That(result, Is.False);
        }

        [Test]
        public void CanFix_NullError_ReturnsFalse()
        {
            AZC0034RuleAnalyzer analyzer = new AZC0034RuleAnalyzer();

            bool result = analyzer.CanFix(null!);

            Assert.That(result, Is.False);
        }

        [Test]
        public void GetFix_ReturnsAgentPromptFixFromProvider()
        {
            AZC0034RuleAnalyzer analyzer = new AZC0034RuleAnalyzer();
            RuleError error = new RuleError("AZC0034", TestErrorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<AgentPromptFix>());
            
            AgentPromptFix promptFix = (AgentPromptFix)fix!;
            Assert.That(promptFix.Action, Is.EqualTo(FixAction.AgentPrompt));
        }

        [Test]
        public void GetFix_UsesAnalyzerPromptProvider()
        {
            AZC0034RuleAnalyzer analyzer = new AZC0034RuleAnalyzer();
            RuleError error = new RuleError("AZC0034", TestErrorMessage);

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
            AZC0034RuleAnalyzer analyzer = new AZC0034RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", TestErrorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Null);
        }

        [Test]
        public void GetFix_NullMessage_ThrowsArgumentException()
        {
            AZC0034RuleAnalyzer analyzer = new AZC0034RuleAnalyzer();

            Assert.Throws<ArgumentNullException>(() => new RuleError("AZC0034", null!));
        }
    }
}
