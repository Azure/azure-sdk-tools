// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// General rule prompts for Azure SDK general analyzers.
    /// </summary>
    internal static partial class AnalyzerPrompts
    {
        /// <summary>
        /// Adds general rule prompts to the builder.
        /// Currently no general rules are defined.
        /// </summary>
        static partial void AddGeneralPrompts(Dictionary<string, AgentPromptFix> builder)
        {
            // TODO: Add general rule prompts here when they are defined
            // Example:
            // builder["CS102"] = new AgentPromptFix("General rule prompt", "General rule context");
        }
    }
}
