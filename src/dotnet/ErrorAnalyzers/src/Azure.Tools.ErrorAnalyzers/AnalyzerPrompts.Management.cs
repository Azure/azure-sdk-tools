// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// Management rule prompts for Azure SDK management library analyzers.
    /// </summary>
    internal static partial class AnalyzerPrompts
    {
        /// <summary>
        /// Adds management rule prompts to the builder.
        /// Currently no management rules are defined.
        /// </summary>
        static void AddManagementPrompts(Dictionary<string, AgentPromptFix> builder)
        {
            // TODO: Add management rule prompts here when they are defined
            // Example:
            // builder["AZC0031"] = new AgentPromptFix("Management rule prompt", "Management rule context");
        }
    }
}
