// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.GeneralRuleAnalyzers
{
    /// <summary>
    /// Provider for general rule analyzers.
    /// This is the primary way to access general-specific rule analyzers.
    /// </summary>
    public sealed class GeneralAnalyzerProvider : IAnalyzerProvider
    {
        private static readonly IReadOnlyList<AgentRuleAnalyzer> generalAnalyzers = new AgentRuleAnalyzer[]
        {
            // Add general analyzers here as they're created
        };

        /// <summary>
        /// Gets all available general rule analyzers.
        /// </summary>
        /// <returns>A collection of initialized analyzers ready to process errors.</returns>
        public IEnumerable<AgentRuleAnalyzer> GetAnalyzers() => generalAnalyzers;
    }
}
