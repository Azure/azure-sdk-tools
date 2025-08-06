// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using NUnit.Framework;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers.Tests
{
    [TestFixture]
    public class AZC0012RuleAnalyzerTests
    {
        private const string BasicErrorMessage = "Type name 'Client' is too generic. Consider using a more descriptive multi-word name, such as 'ServiceClient'.";
        private const string ComplexErrorMessage = "Type name 'Data' is too generic. Consider using a more descriptive multi-word name, such as 'UserData'.";

        [Test]
        public void CanFix_ValidAZC0012Error_ReturnsTrue()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", BasicErrorMessage);

            bool result = analyzer.CanFix(error);

            Assert.That(result, Is.True);
        }

        [Test]
        public void CanFix_InvalidErrorType_ReturnsFalse()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0001", "Some other error message");

            bool result = analyzer.CanFix(error);

            Assert.That(result, Is.False);
        }

        [Test]
        public void CanFix_NullError_ReturnsFalse()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();

            bool result = analyzer.CanFix(null!);

            Assert.That(result, Is.False);
        }

        [Test]
        public void GetFix_BasicPattern_ReturnsAgentPromptFix()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", BasicErrorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<AgentPromptFix>());
            
            AgentPromptFix promptFix = (AgentPromptFix)fix!;
            Assert.That(promptFix.Action, Is.EqualTo(FixAction.AgentPrompt));
            Assert.That(promptFix.Prompt, Does.Contain("Client"));
            Assert.That(promptFix.Prompt, Does.Contain("ServiceClient"));
            Assert.That(promptFix.Context, Does.Contain("AZC0012"));
        }

        [Test]
        public void GetFix_ComplexTypeName_ReturnsAgentPromptFix()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", ComplexErrorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<AgentPromptFix>());
            
            AgentPromptFix promptFix = (AgentPromptFix)fix!;
            Assert.That(promptFix.Prompt, Does.Contain("Data"));
            Assert.That(promptFix.Prompt, Does.Contain("UserData"));
            Assert.That(promptFix.Context, Does.Contain("AZC0012"));
        }

        [Test]
        public void GetFix_MessageWithoutSuggestion_ReturnsGenericPrompt()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", "Type name 'Helper' is too generic and needs improvement.");

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<AgentPromptFix>());
            
            AgentPromptFix promptFix = (AgentPromptFix)fix!;
            Assert.That(promptFix.Prompt, Does.Contain("Helper"));
            Assert.That(promptFix.Prompt, Does.Contain("NAMING EXAMPLES"));
            Assert.That(promptFix.Context, Does.Contain("AZC0012"));
        }

        [Test]
        public void GetFix_MessageWithoutTypeNamePattern_ReturnsGenericPrompt()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", "Generic type name detected, please fix it.");

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<AgentPromptFix>());
            
            AgentPromptFix promptFix = (AgentPromptFix)fix!;
            Assert.That(promptFix.Prompt, Does.Contain("TASK: Fix AZC0012"));
            Assert.That(promptFix.Prompt, Does.Contain("NAMING EXAMPLES"));
            Assert.That(promptFix.Context, Does.Contain("AZC0012"));
        }

        [Test]
        public void GetFix_InvalidErrorType_ReturnsNull()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0001", BasicErrorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Null);
        }

        [Test]
        public void GetFix_GenericMessage_ReturnsGenericPrompt()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", "Generic type name issue");

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<AgentPromptFix>());
            
            AgentPromptFix promptFix = (AgentPromptFix)fix!;
            Assert.That(promptFix.Prompt, Does.Contain("TASK: Fix AZC0012"));
        }

        [Test]
        public void AgentPromptFix_ContainsRelevantInstructions()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", BasicErrorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.InstanceOf<AgentPromptFix>());
            AgentPromptFix promptFix = (AgentPromptFix)fix!;
            
            // Check that the prompt contains useful instructions
            Assert.That(promptFix.Prompt, Does.Contain("TASK:"));
            Assert.That(promptFix.Prompt, Does.Contain("INSTRUCTIONS:"));
            Assert.That(promptFix.Prompt.ToLower(), Does.Contain("rename"));
            
            // Check that context is provided
            Assert.That(promptFix.Context, Is.Not.Null);
            Assert.That(promptFix.Context, Does.Contain("RULE: AZC0012"));
            Assert.That(promptFix.Context, Does.Contain("ORIGINAL ERROR:"));
        }
    }
}
