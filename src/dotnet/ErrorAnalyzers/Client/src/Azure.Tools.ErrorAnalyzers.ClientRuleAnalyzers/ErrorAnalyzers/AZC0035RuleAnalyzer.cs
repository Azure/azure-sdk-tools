// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers
{
    /// <summary>
    /// Handles AZC0035: Missing model factory methods.
    /// </summary>
    [DiscoverableAnalyzer]
    internal sealed class AZC0035RuleAnalyzer : BasePromptAnalyzer
    {
        public AZC0035RuleAnalyzer() : base("AZC0035") { }
    }
}
