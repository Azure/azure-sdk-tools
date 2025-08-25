// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers
{
    /// <summary>
    /// Provider for client rule analyzers that creates analyzers for all supported AZC rules.
    /// This provider automatically discovers and creates analyzers based on the <see cref="AzcRuleType"/> enum values.
    /// </summary>
    public sealed class ClientAnalyzerProvider : AnalyzerProviderBase
    {
        /// <inheritdoc/>
        protected override IReadOnlyList<AgentRuleAnalyzer> DiscoverAnalyzers()
        {
            ArgumentNullException.ThrowIfNull(GetAnalyzerAssembly());

            // Get all rule types and create an analyzer for each
            var ruleTypes = Enum.GetValues<AzcRuleType>();
            var analyzers = new AgentRuleAnalyzer[ruleTypes.Length];

            for (int i = 0; i < ruleTypes.Length; i++)
            {
                analyzers[i] = CreateAnalyzer(ruleTypes[i]);
            }

            return analyzers;
        }

        /// <summary>
        /// Creates a new analyzer instance for the specified rule type.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AgentRuleAnalyzer CreateAnalyzer(AzcRuleType ruleType)
        {
            return new BasePromptAnalyzer(ruleType);
        }
    }
}