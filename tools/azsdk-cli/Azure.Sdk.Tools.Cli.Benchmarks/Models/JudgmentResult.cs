// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Result of an LLM judgment.
/// </summary>
public class JudgmentResult
{
    /// <summary>
    /// Gets whether the judgment passed.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Gets the reasoning provided by the LLM.
    /// </summary>
    public required string Reasoning { get; init; }
}
