// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Frozen;

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// Provides access to prompts and context for error analysis.
    /// This class contains prompt and context data for generating AI agent instructions.
    /// </summary>
    internal static partial class AnalyzerPrompts
    {
        /// <summary>
        /// The rule ID used for fallback analysis of unknown error types.
        /// </summary>
        internal const string FallbackRuleId = "__FALLBACK__";

        /// <summary>
        /// Gets the frozen dictionary of all available prompts for maximum performance.
        /// </summary>
        private static readonly FrozenDictionary<string, AgentPromptFix> AllPrompts = CreateAllPrompts();

        /// <summary>
        /// Gets the prompt and context for the specified rule ID.
        /// Returns fallback prompt for unknown rule IDs.
        /// </summary>
        internal static AgentPromptFix GetPromptFix(string ruleId)
        {
            if (string.IsNullOrEmpty(ruleId))
            {
                return AllPrompts[FallbackRuleId];
            }
            
            return AllPrompts.TryGetValue(ruleId, out var fix) ? fix : AllPrompts[FallbackRuleId];
        }

        /// <summary>
        /// Gets all available rule IDs.
        /// </summary>
        internal static IReadOnlyCollection<string> GetAllRuleIds()
        {
            return AllPrompts.Keys;
        }

        /// <summary>
        /// Creates and freezes the complete dictionary of all prompts from all categories.
        /// </summary>
        private static FrozenDictionary<string, AgentPromptFix> CreateAllPrompts()
        {
            var builder = new Dictionary<string, AgentPromptFix>(StringComparer.OrdinalIgnoreCase);

            // Add prompts from all partial classes
            AddClientPrompts(builder);
            AddGeneralPrompts(builder);
            AddManagementPrompts(builder);

            return builder.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }
    }
}
