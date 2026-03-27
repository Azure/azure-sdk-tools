// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Tracks token usage from LLM interactions during a benchmark execution.
/// </summary>
public class TokenUsage
{
    /// <summary>Gets or sets the total input (prompt) tokens consumed.</summary>
    public double InputTokens { get; set; }

    /// <summary>Gets or sets the total output (completion) tokens consumed.</summary>
    public double OutputTokens { get; set; }

    /// <summary>Gets or sets the total cache-read tokens (prompt tokens served from cache).</summary>
    public double CacheReadTokens { get; set; }

    /// <summary>Gets or sets the total cache-write tokens (prompt tokens written to cache).</summary>
    public double CacheWriteTokens { get; set; }

    /// <summary>Gets the total tokens consumed (input + output + cache read + cache write).</summary>
    public double TotalTokens => InputTokens + OutputTokens + CacheReadTokens + CacheWriteTokens;

    /// <summary>
    /// Adds another <see cref="TokenUsage"/> instance into this one (mutating).
    /// </summary>
    public void Add(TokenUsage other)
    {
        InputTokens += other.InputTokens;
        OutputTokens += other.OutputTokens;
        CacheReadTokens += other.CacheReadTokens;
        CacheWriteTokens += other.CacheWriteTokens;
    }

    /// <summary>
    /// Returns a new <see cref="TokenUsage"/> that is the sum of two instances.
    /// </summary>
    public static TokenUsage operator +(TokenUsage a, TokenUsage b)
    {
        return new TokenUsage
        {
            InputTokens = a.InputTokens + b.InputTokens,
            OutputTokens = a.OutputTokens + b.OutputTokens,
            CacheReadTokens = a.CacheReadTokens + b.CacheReadTokens,
            CacheWriteTokens = a.CacheWriteTokens + b.CacheWriteTokens
        };
    }
}
