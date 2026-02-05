// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

/// <summary>
/// Validates that the actual git diff achieves the same effect as an expected diff.
/// Uses an LLM judge to compare the diffs semantically.
/// </summary>
public class ExpectedDiffValidator : IValidator
{
    private const string SystemPrompt = """
        You are a code diff validator. Your job is to determine if two git diffs achieve the exact same effect.
        
        Consider:
        - Do they modify the same file(s)?
        - Do they make changes at the same logical location (same function, class, or code block)?
        - Do they achieve the same semantic outcome?
        
        Minor differences that are acceptable:
        - Different line numbers (if the logical location is the same)
        - Whitespace differences
        - Slightly different context lines
        
        Respond with exactly one of:
        - PASS: <brief explanation of why the diffs match>
        - FAIL: <brief explanation of what's different>
        """;

    /// <summary>
    /// Gets the name of this validator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the expected diff (with context).
    /// </summary>
    public string ExpectedDiff { get; }

    /// <summary>
    /// Gets the number of context lines to capture in the actual diff.
    /// </summary>
    public int ContextLines { get; init; } = 10;

    /// <summary>
    /// Gets the model to use for judging.
    /// </summary>
    public string Model { get; init; } = "claude-sonnet-4.5";

    /// <summary>
    /// Creates a new expected diff validator.
    /// </summary>
    /// <param name="name">Human-readable name for the validator.</param>
    /// <param name="expectedDiff">The expected git diff.</param>
    public ExpectedDiffValidator(string name, string expectedDiff)
    {
        Name = name;
        ExpectedDiff = expectedDiff;
    }

    public async Task<ValidationResult> ValidateAsync(
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 1. Re-capture actual diff with configured context
        var actualDiff = await context.Workspace.GetGitDiffAsync(ContextLines);

        if (string.IsNullOrWhiteSpace(actualDiff))
        {
            stopwatch.Stop();
            return new ValidationResult
            {
                ValidatorName = Name,
                Passed = false,
                Message = "No changes detected in workspace",
                Duration = stopwatch.Elapsed
            };
        }

        // 2. Build the user prompt
        var userPrompt = $"""
            ## Expected Diff
            ```diff
            {ExpectedDiff}
            ```

            ## Actual Diff
            ```diff
            {actualDiff}
            ```

            Do these diffs achieve the exact same effect?
            """;

        // 3. Call LLM judge
        try
        {
            using var judge = new LlmJudge();
            var judgment = await judge.JudgeAsync(SystemPrompt, userPrompt, Model, cancellationToken);

            stopwatch.Stop();
            return new ValidationResult
            {
                ValidatorName = Name,
                Passed = judgment.Passed,
                Message = judgment.Passed ? "Diff matches expected" : "Diff does not match expected",
                Details = judgment.Reasoning,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ValidationResult
            {
                ValidatorName = Name,
                Passed = false,
                Message = $"LLM judge failed: {ex.Message}",
                Duration = stopwatch.Elapsed
            };
        }
    }
}
