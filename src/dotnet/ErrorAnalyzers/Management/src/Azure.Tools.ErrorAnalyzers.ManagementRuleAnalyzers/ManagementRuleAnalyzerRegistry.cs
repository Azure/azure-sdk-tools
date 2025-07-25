// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.ManagementRuleAnalyzers
{
    /// <summary>
    /// Provider for management rule analyzers.
    /// This is the primary way to access management-specific rule analyzers.
    /// </summary>
    public sealed class ManagementAnalyzerProvider : IAnalyzerProvider
    {
        private static readonly IReadOnlyList<AgentRuleAnalyzer> managementAnalyzers = new AgentRuleAnalyzer[]
        {
            // Add management analyzers here as they're created
        };

        /// <summary>
        /// Gets all available management rule analyzers.
        /// </summary>
        /// <returns>A collection of initialized analyzers ready to process errors.</returns>
        public IEnumerable<AgentRuleAnalyzer> GetAnalyzers() => managementAnalyzers;
    }
}
