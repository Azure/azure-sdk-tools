// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.RegularExpressions;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers
{
    /// <summary>
    /// Handles AZC0012: “Type name '{0}' is too generic… Consider using a more descriptive multi-word name, such as '{1}'.”
    /// </summary>
    internal sealed class AZC0012RuleAnalyzer : AgentRuleAnalyzer
    {
        private static readonly Regex ErrorMessageRegex = new Regex(
            @"Type name\s+'(?<original>[^']+)'\s+is too generic.*?such as\s+'(?<suggest>[^']+)'",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5));
        
        public override bool CanFix(RuleError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return string.Equals(error.type, "AZC0012", StringComparison.OrdinalIgnoreCase);
        }

        public override Fix? GetFix(RuleError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            
            if (!CanFix(error))
            {
                return null;
            }

            Match match = ErrorMessageRegex.Match(error.message);
            
            if (!match.Success)
            {
                return null;
            }
            
            string originalName = match.Groups["original"].Value;
            string suggestedName = match.Groups["suggest"].Value;

            if (string.IsNullOrWhiteSpace(originalName) || string.IsNullOrWhiteSpace(suggestedName))
            {
                return null;
            }

            return new RenameFix(originalName, suggestedName);
        }
    }
}
