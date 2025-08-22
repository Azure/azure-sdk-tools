// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// Base class for analyzers that use prompts from AnalyzerPromptProvider.
    /// </summary>
    internal abstract class BasePromptAnalyzer : AgentRuleAnalyzer
    {
        private readonly string _ruleId;

        /// <summary>
        /// Initializes a new instance of the <see cref="BasePromptAnalyzer"/> class.
        /// </summary>
        protected BasePromptAnalyzer(string ruleId)
        {
            _ruleId = ruleId ?? throw new ArgumentNullException(nameof(ruleId));
        }

        /// <inheritdoc/>
        public override bool CanFix(RuleError error)
        {
            if (error?.type == null)
                return false;

            return string.Equals(error.type, _ruleId, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public override Fix? GetFix(RuleError error)
        {
            if (error?.message == null)
                return null;

            if (!CanFix(error))
                return null;

            if (!AnalyzerPromptProvider.TryGetPrompt(_ruleId, out var prompt))
                return null;

            if (!AnalyzerPromptProvider.TryGetContext(_ruleId, error.message, out var context))
                return null;

            return new AgentPromptFix(prompt, context);
        }
    }
}
