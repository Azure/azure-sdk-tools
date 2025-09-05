// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// Main entry point for the Error Analyzer library.
    /// Provides a unified interface for analyzing errors and generating fixes.
    /// </summary>
    public static class ErrorAnalyzerService
    {
        /// <summary>
        /// Attempts to generate a fix for a single error.
        /// Uses fallback mechanism for unknown error types.
        /// </summary>
        public static Fix GetFix(RuleError error)
        {
            ArgumentNullException.ThrowIfNull(error);

            // Get fix with automatic fallback for unknown rules
            var fix = AnalyzerPrompts.GetPromptFix(error.type);

            // Format context if available, otherwise keep it null
            string? context = null;
            if (!string.IsNullOrEmpty(fix.Context))
            {
                try
                {
                    context = string.Format(System.Globalization.CultureInfo.InvariantCulture, fix.Context, error.message);
                }
                catch (FormatException)
                {
                    // If formatting fails, use original context without formatting
                    context = fix.Context;
                }
            }

            return new AgentPromptFix(fix.Prompt, context);
        }

        /// <summary>
        /// Attempts to generate fixes for multiple errors.
        /// Optimized for batch processing with direct dictionary access.
        /// </summary>
        public static IEnumerable<Fix> GetFixes(IEnumerable<RuleError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            foreach (var error in errors)
            {
                yield return GetFix(error);
            }
        }

        /// <summary>
        /// Gets all available rule IDs.
        /// Returns all compile-time registered rules for maximum compatibility.
        /// </summary>
        public static IEnumerable<string> GetRegisteredRules()
        {
            // Return all available rule IDs from the compile-time dictionary
            // Exclude the fallback mechanism from the public API
            return AnalyzerPrompts.GetAllRuleIds()
                .Where(id => id != AnalyzerPrompts.FallbackRuleId);
        }
    }
}
