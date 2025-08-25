// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers
{
    /// <summary>
    /// Defines the supported Azure Client (AZC) rule types.
    /// </summary>
    internal enum AzcRuleType
    {
        /// <summary>
        /// Detects generic type names that have high chance of collision with BCL types.
        /// </summary>
        AZC0012 = 0,

        /// <summary>
        /// Detects model naming suffix issues.
        /// </summary>
        AZC0030 = 1,

        /// <summary>
        /// Detects type name conflicts.
        /// </summary>
        AZC0034 = 2,

        /// <summary>
        /// Detects missing model factory methods.
        /// </summary>
        AZC0035 = 3
    }
}