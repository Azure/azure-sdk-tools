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
        /// Gets the frozen dictionary of all available prompts for maximum performance.
        /// </summary>
        private static readonly FrozenDictionary<string, AgentPromptFix> AllPrompts = CreateAllPrompts();

        /// <summary>
        /// Tries to get the prompt and context for the specified rule ID.
        /// </summary>
        internal static bool TryGetPromptFix(string ruleId, out AgentPromptFix? fix)
        {
            return AllPrompts.TryGetValue(ruleId, out fix);
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
