// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

/// <summary>
/// Validates that a file contains all specified patterns.
/// </summary>
public class ContainsValidator : IValidator
{
    public string Name { get; }
    
    /// <summary>
    /// Gets the file path relative to the repo root.
    /// </summary>
    public string FilePath { get; }
    
    /// <summary>
    /// Gets the patterns that must be present in the file.
    /// </summary>
    public IReadOnlyList<string> Patterns { get; }
    
    /// <summary>
    /// Gets whether pattern matching is case-sensitive (default: true).
    /// </summary>
    public bool CaseSensitive { get; }

    public ContainsValidator(
        string name,
        string filePath,
        IEnumerable<string> patterns,
        bool caseSensitive = true)
    {
        Name = name;
        FilePath = filePath;
        Patterns = patterns.ToList();
        CaseSensitive = caseSensitive;
    }

    public async Task<ValidationResult> ValidateAsync(
        ValidationContext context, 
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(context.RepoPath, FilePath);

        if (!File.Exists(fullPath))
        {
            return ValidationResult.Fail(Name, $"File not found: {FilePath}");
        }

        var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
        var searchContent = CaseSensitive ? content : content.ToLowerInvariant();

        var missingPatterns = new List<string>();
        foreach (var pattern in Patterns)
        {
            var searchPattern = CaseSensitive ? pattern : pattern.ToLowerInvariant();
            if (!searchContent.Contains(searchPattern))
            {
                missingPatterns.Add(pattern);
            }
        }

        if (missingPatterns.Count == 0)
        {
            return ValidationResult.Pass(Name, $"All {Patterns.Count} pattern(s) found");
        }

        return ValidationResult.Fail(Name, 
            $"Missing patterns: {string.Join(", ", missingPatterns)}",
            $"File: {FilePath}");
    }
}
