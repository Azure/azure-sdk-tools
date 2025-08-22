// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// A fix that provides instructions to an AI agent.
    /// </summary>
    public sealed class AgentPromptFix : Fix
    {
        public AgentPromptFix(string prompt, string? context = null) 
            : base(FixAction.AgentPrompt)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
            Prompt = prompt;
            Context = context ?? string.Empty;
        }

        /// <summary>
        /// The instruction prompt for the AI agent.
        /// </summary>
        public string Prompt { get; }

        /// <summary>
        /// Optional additional context for the agent.
        /// </summary>
        public string Context { get; }
    }
}
