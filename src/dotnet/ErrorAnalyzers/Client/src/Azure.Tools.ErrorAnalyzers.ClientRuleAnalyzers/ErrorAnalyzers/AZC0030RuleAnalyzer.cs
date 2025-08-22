// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers
{
    /// <summary>
    /// Handles AZC0030: Model naming suffix issues.
    /// </summary>
    [DiscoverableAnalyzer]
    internal sealed class AZC0030RuleAnalyzer : BasePromptAnalyzer
    {
        public AZC0030RuleAnalyzer() : base("AZC0030") { }
    }
}
