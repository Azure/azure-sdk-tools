// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// The shape of an individual fix instruction.
    /// </summary>
    public enum FixAction
    {
        /// <summary>
        /// Instruct the AI agent with a prompt snippet.
        /// </summary>
        AgentPrompt,

        /// <summary>
        /// A simple in‐code rename, containing Context = from, Action = to.
        /// </summary>
        Rename,
    }
}
