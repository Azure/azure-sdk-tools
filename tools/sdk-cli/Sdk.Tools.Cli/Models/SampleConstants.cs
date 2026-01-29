// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Sdk.Tools.Cli.Models;

/// <summary>
/// Shared constants for sample generation tools.
/// </summary>
public static class SampleConstants
{
    /// <summary>
    /// Maximum total characters to load for all context (source + samples).
    /// Sized to fit within AI model token limits (~100K tokens ≈ 400K chars).
    /// </summary>
    public const int MaxContextCharacters = 400_000;

    /// <summary>
    /// Maximum number of characters per file when loading context.
    /// </summary>
    public const int MaxCharactersPerFile = 50_000;
    
    /// <summary>
    /// Budget allocation for source code (60% of total).
    /// Source code is more important for generating new samples.
    /// </summary>
    public const int SourceCodeBudget = 240_000;
    
    /// <summary>
    /// Budget allocation for existing samples (40% of total).
    /// Samples help avoid duplicates and show patterns.
    /// </summary>
    public const int ExistingSamplesBudget = 160_000;

    /// <summary>
    /// Default batch size for processing samples.
    /// </summary>
    public const int DefaultBatchSize = 5;
    
    /// <summary>
    /// Priority for existing samples (lower = higher priority, loaded first).
    /// Samples are prioritized over source code to avoid duplicating them.
    /// </summary>
    public const int ExistingSamplesPriority = 1;
    
    /// <summary>
    /// Priority for source code files (loaded after existing samples).
    /// </summary>
    public const int SourceCodePriority = 10;
    
    /// <summary>
    /// Glob patterns to exclude when loading existing samples.
    /// </summary>
    public static readonly string[] ExistingSamplesExcludePatterns = 
    {
        "**/obj/**",
        "**/bin/**",
        "**/*.csproj",
        "**/*.targets"
    };
}
