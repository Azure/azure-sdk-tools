// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// Interface for providing analyzers to the ErrorAnalyzerService.
    /// This enables explicit registration and better testability.
    /// </summary>
    public interface IAnalyzerProvider
    {
        /// <summary>
        /// Gets all analyzers provided by this provider.
        /// </summary>
        /// <returns>A collection of analyzers.</returns>
        IEnumerable<AgentRuleAnalyzer> GetAnalyzers();
    }
}