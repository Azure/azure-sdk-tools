// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers
{
    /// <summary>
    /// Handles AZC0012: Generic type name violations.
    /// </summary>
    [DiscoverableAnalyzer]
    internal sealed class AZC0012RuleAnalyzer : AgentRuleAnalyzer
    {
        public override bool CanFix(RuleError error)
        {
            if (error?.type == null) 
                return false;

            return string.Equals(error.type, "AZC0012", StringComparison.OrdinalIgnoreCase);
        }

        public override Fix? GetFix(RuleError error)
        {
            if (error?.message == null)
                throw new ArgumentException("Error message cannot be null.", nameof(error));
            if (!CanFix(error))
            {
                return null;
            }

            // TODO: figure out a way to move the hard coded prompts and context into a configurable setup.
            string prompt = "TASK: Fix AZC0012 Generic Type Name Violation\n\n" +
                           "The analyzer has detected a generic type name that violates Azure SDK naming conventions.\n\n" +
                           "INSTRUCTIONS:\n" +
                           "1. Read the error message to identify the generic type name\n" +
                           "2. If a suggestion is provided, use it; otherwise choose a descriptive alternative\n" +
                           "3. Rename the type to be more specific and descriptive\n" +
                           "4. Update all references, imports, and documentation\n" +
                           "5. Ensure the new name follows Azure SDK naming conventions\n\n" +
                           "NAMING EXAMPLES:\n" +
                           "• 'Client' → 'BlobServiceClient', 'TableServiceClient', 'KeyVaultClient'\n" +
                           "• 'Manager' → 'ResourceManager', 'ConnectionManager', 'CacheManager'\n" +
                           "• 'Helper' → 'ValidationHelper', 'SerializationHelper', 'CryptoHelper'\n" +
                           "• 'Service' → 'StorageService', 'AuthenticationService', 'NotificationService'\n" +
                           "• 'Data' → 'UserData', 'ConfigurationData', 'MetricData'\n\n" +
                           "Choose a name that clearly describes the type's purpose and responsibility.";

            string context = $"RULE: AZC0012 - Avoid generic type names\n" +
                           $"ORIGINAL ERROR: {error.message}\n" +
                           $"BACKGROUND: Generic type names don't provide enough context about what the type does. " +
                           $"Azure SDK guidelines require descriptive, multi-word names that make code self-documenting.";

            return new AgentPromptFix(prompt, context);
        }
    }
}
