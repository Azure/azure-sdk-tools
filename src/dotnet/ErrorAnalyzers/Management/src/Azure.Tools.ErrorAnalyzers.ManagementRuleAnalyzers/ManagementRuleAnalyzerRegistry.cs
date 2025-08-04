// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.ManagementRuleAnalyzers
{
    /// <summary>
    /// Provider for management rule analyzers.
    /// Automatically discovers all marked analyzer implementations in this assembly.
    /// </summary>
    public sealed class ManagementAnalyzerProvider : AnalyzerProviderBase
    {
        // Base class handles all discovery logic automatically

    }
}
