// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
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
        private static readonly ConcurrentBag<IAnalyzerProvider> AnalyzerProviders = new ConcurrentBag<IAnalyzerProvider>();

        /// <summary>
        /// Attempts to generate fixes for multiple errors.
        /// </summary>
        public static IEnumerable<Fix> GetFixes(IEnumerable<RuleError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            foreach (RuleError error in errors)
            {
                Fix? fix = GetFix(error);
                if (fix != null)
                {
                    yield return fix;
                }
            }
        }

        /// <summary>
        /// Attempts to generate a fix for a single error.
        /// </summary>
        public static Fix? GetFix(RuleError error)
        {
            ArgumentNullException.ThrowIfNull(error);

            foreach (AgentRuleAnalyzer analyzer in GetAllAnalyzers())
            {
                if (analyzer.CanFix(error))
                {
                    return analyzer.GetFix(error);
                }
            }
            return null;
        }

        /// <summary>
        /// Gets all available analyzers from all registries.
        /// </summary>
        public static IEnumerable<AgentRuleAnalyzer> GetAllAnalyzers()
        {
            return AllAnalyzers.Value;
        }

        private static readonly Lazy<IReadOnlyList<AgentRuleAnalyzer>> AllAnalyzers =
            new Lazy<IReadOnlyList<AgentRuleAnalyzer>>(CreateAllAnalyzers);

        private static IReadOnlyList<AgentRuleAnalyzer> CreateAllAnalyzers()
        {
            List<AgentRuleAnalyzer> allAnalyzers = new List<AgentRuleAnalyzer>();

            foreach (IAnalyzerProvider provider in AnalyzerProviders)
            {
                try
                {
                    allAnalyzers.AddRange(provider.GetAnalyzers());
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return allAnalyzers.AsReadOnly();
        }

        /// <summary>
        /// Registers an analyzer provider. This should be called during application startup.
        /// This method is thread-safe and can be called from multiple threads.
        /// </summary>
        public static void RegisterProvider(IAnalyzerProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            AnalyzerProviders.Add(provider);
        }
    }
}
