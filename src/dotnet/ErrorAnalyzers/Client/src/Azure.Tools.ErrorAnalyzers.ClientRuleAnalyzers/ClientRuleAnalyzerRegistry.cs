// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers
{
    /// <summary>
    /// Provider for client rule analyzers.
    /// This is the primary way to access client-specific rule analyzers.
    /// </summary>
    public sealed class ClientAnalyzerProvider : IAnalyzerProvider
    {
        private static readonly IReadOnlyList<AgentRuleAnalyzer> clientAnalyzers = new AgentRuleAnalyzer[]
        {
            new AZC0012RuleAnalyzer(),
            // Add new analyzers here as they're created
        };

        /// <summary>
        /// Gets all available client rule analyzers.
        /// </summary>
        /// <returns>A collection of initialized analyzers ready to process errors.</returns>
        public IEnumerable<AgentRuleAnalyzer> GetAnalyzers() => clientAnalyzers;
    }
}