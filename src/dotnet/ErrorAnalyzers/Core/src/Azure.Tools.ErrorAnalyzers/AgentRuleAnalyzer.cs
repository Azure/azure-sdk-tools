// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// Base class for any rule‐specific analyzer that can produce fixes.
    /// Implementers should focus on a specific error type or pattern and provide automated fixes.
    /// </summary>
    public abstract class AgentRuleAnalyzer
    {
        /// <summary>
        /// Determines if this analyzer can provide a fix for the specified error.
        /// </summary>
        public abstract bool CanFix(RuleError error);

        /// <summary>
        /// Generates a fix for the specified error, if possible.
        /// </summary>
        public abstract Fix? GetFix(RuleError error);
    }
}
