// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.CopilotAgents;

/// <summary>
/// Controls whether the next turn uses the same conversation or returns a caller-owned result.
/// </summary>
public sealed record CopilotAgentTurnResult<TResult>(bool Continue, string? Prompt, TResult? Result)
    where TResult : notnull;
