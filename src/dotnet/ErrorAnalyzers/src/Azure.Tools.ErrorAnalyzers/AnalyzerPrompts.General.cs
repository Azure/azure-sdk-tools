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
        /// Includes fallback mechanisms for unknown error types.
        /// </summary>
        static void AddGeneralPrompts(Dictionary<string, AgentPromptFix> builder)
        {
            // Fallback prompt for unknown/unhandled errors from any source
            builder[FallbackRuleId] = new AgentPromptFix(
               prompt: "TASK: Analyze and fix unknown error\nACTION: Identify error source and apply appropriate fix in client.tsp to resolve the error\nSOURCES: TypeSpec (syntax/semantic), Azure Analyzer (AZC####), MSBuild\nAPPROACH: Read error message → Identify problem → Determine what changes need to be made in client.tsp to fix it",
               context: "FALLBACK - Unknown error requiring analysis\nERROR: {0}"
            );

            // TODO: Add other general rule prompts here when they are defined
            // Example:
            // builder["CS102"] = new AgentPromptFix("General rule prompt", "General rule context");
        }
    }
}
