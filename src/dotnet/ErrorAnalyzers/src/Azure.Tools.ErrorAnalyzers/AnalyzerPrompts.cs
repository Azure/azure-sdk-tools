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
        /// Tries to get just the prompt for the specified rule ID.
        /// Provided for backward compatibility with tests.
        /// </summary>
        internal static bool TryGetPrompt(string ruleId, out string prompt)
        {
            prompt = string.Empty;

            if (ruleId is null or { Length: 0 })
            {
                return false;
            }

            if (AllPrompts.TryGetValue(ruleId, out var fix))
            {
                prompt = fix.Prompt;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to get the formatted context for the specified rule ID and error message.
        /// Provided for backward compatibility with tests.
        /// </summary>
        internal static bool TryGetContext(string ruleId, string errorMessage, out string? context)
        {
            context = null;

            if (ruleId is null or { Length: 0 } || string.IsNullOrWhiteSpace(errorMessage))
            {
                return false;
            }

            if (!AllPrompts.TryGetValue(ruleId, out var fix))
            {
                return false;
            }

            // Handle null or empty context from data - both mean "no context available"
            if (string.IsNullOrEmpty(fix.Context))
            {
                context = null; // No context available
                return true;
            }

            try
            {
                context = string.Format(System.Globalization.CultureInfo.InvariantCulture, fix.Context, errorMessage);
                return true;
            }
            catch (FormatException)
            {
                // If formatting fails, return the original context without formatting
                context = fix.Context;
                return true;
            }
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

        /// <summary>
        /// Adds client (AZC) rule prompts to the builder.
        /// Implemented in AnalyzerPrompts.Client.cs
        /// </summary>
        static partial void AddClientPrompts(Dictionary<string, AgentPromptFix> builder);

        /// <summary>
        /// Adds general rule prompts to the builder.
        /// Implemented in AnalyzerPrompts.General.cs
        /// </summary>
        static partial void AddGeneralPrompts(Dictionary<string, AgentPromptFix> builder);

        /// <summary>
        /// Adds management rule prompts to the builder.
        /// Implemented in AnalyzerPrompts.Management.cs
        /// </summary>
        static partial void AddManagementPrompts(Dictionary<string, AgentPromptFix> builder);
    }
}
