// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers
{
    /// <summary>
    /// Base class for analyzers that use prompts from AnalyzerPromptProvider.
    /// </summary>
    [DiscoverableAnalyzer]
    internal sealed class BasePromptAnalyzer : AgentRuleAnalyzer
    {
        /// <summary>
        /// Gets the rule type this analyzer handles.
        /// </summary>
        public AzcRuleType RuleType { get; }

        /// <summary>
        /// Cached rule type string to avoid repeated ToString() calls.
        /// </summary>
        private readonly string RuleTypeString;

        /// <summary>
        /// Initializes a new instance of the <see cref="BasePromptAnalyzer"/> class.
        /// </summary>
        public BasePromptAnalyzer(AzcRuleType ruleType)
        {
            RuleType = ruleType;
            RuleTypeString = ruleType.ToString();
        }

        /// <inheritdoc/>
        public override bool CanFix(RuleError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            ArgumentNullException.ThrowIfNull(error.type);

            return string.Equals(error.type, RuleTypeString, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public override Fix? GetFix(RuleError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            ArgumentNullException.ThrowIfNull(error.message);

            if (!CanFix(error) ||
                !AnalyzerPromptProvider.TryGetPrompt(RuleTypeString, out var prompt) ||
                !AnalyzerPromptProvider.TryGetContext(RuleTypeString, error.message, out var context))
            {
                return null;
            }

            return new AgentPromptFix(prompt, context);
        }

        public override string ToString() => $"Rule Analyzer: {RuleType}";
    }
}
