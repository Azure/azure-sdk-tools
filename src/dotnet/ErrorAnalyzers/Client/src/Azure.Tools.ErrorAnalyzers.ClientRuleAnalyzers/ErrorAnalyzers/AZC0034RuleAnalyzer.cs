// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers
{
    /// <summary>
    /// Handles AZC0034: Type name conflicts.
    /// </summary>
    [DiscoverableAnalyzer]
    internal sealed class AZC0034RuleAnalyzer : BasePromptAnalyzer
    {
        public AZC0034RuleAnalyzer() : base("AZC0034") { }
    }
}
