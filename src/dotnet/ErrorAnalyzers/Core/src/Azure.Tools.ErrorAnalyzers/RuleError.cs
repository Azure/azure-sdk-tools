// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// A normalized error pulled from your build/logs.
    /// </summary>
    public class RuleError
    {
        public RuleError(string type, string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(type);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            
            this.type = type;
            this.message = message;
        }

        /// <summary>
        /// e.g. "AZC0012" or "AZC0030"
        /// </summary>
        public string type { get; init; }

        /// <summary>
        /// The full error line/message from the compiler or analyzer.
        /// </summary>
        public string message { get; init; }
    }
}
