// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// Client (AZC) rule prompts for Azure SDK client library analyzers.
    /// </summary>
    internal static partial class AnalyzerPrompts
    {
        /// <summary>
        /// Adds client rule prompts to the builder.
        /// </summary>
        static void AddClientPrompts(Dictionary<string, AgentPromptFix> builder)
        {
            // AZC0030 - Model Naming Suffix Violation
            builder["AZC0030"] = new AgentPromptFix(
                prompt: "AZC0030 TASK: Fix model naming suffix\nACTION: Add @@clientName decorator with proper suffix\nSYNTAX: @@clientName(ModelName, \"NewNameWithSuffix\", \"csharp\")\nSUFFIX PATTERNS: Input→'Options', Response→'Result', Data→'Info'\nEXAMPLE: @@clientName(UserModel, \"UserData\", \"csharp\")",
                context: "AZC0030 - Model naming conventions\nERROR: {0}"
            );

            // AZC0034 - Type Name Conflict
            builder["AZC0034"] = new AgentPromptFix(
                prompt: "AZC0034 TASK: Resolve type name conflict\nACTION: Add @@clientName decorator to avoid conflicts\nSYNTAX: @@clientName(ConflictingType, \"UniqueNonConflictingName\", \"csharp\")\nEXAMPLE: @@clientName(Response, \"CreateBlobResponse\", \"csharp\")\nSTRATEGY: Add service/operation context to make unique",
                context: "AZC0034 - Type name conflict\nERROR: {0}"
            );

            // AZC0035 - Missing Model Factory Method
            builder["AZC0035"] = new AgentPromptFix(
                prompt: "AZC0035 TASK: Enable model factory method generation\nACTION: Add @@usage decorator to mark as output-only\nSYNTAX: @@usage(ModelName, Usage.output)\nEXAMPLE: @@usage(UserInfo, Usage.output)\nNOTE: This generates factory methods for testing scenarios",
                context: "AZC0035 - Missing model factory method\nERROR: {0}"
            );
        }
    }
}
