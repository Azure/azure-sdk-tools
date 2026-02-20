// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

/// <summary>
/// Validates that specified files exist in the workspace.
/// </summary>
public class FileExistsValidator : IValidator
{
    public string Name { get; }
    
    /// <summary>
    /// Gets the file paths (relative to repo root) that must exist.
    /// </summary>
    public IReadOnlyList<string> FilePaths { get; }

    public FileExistsValidator(string name, IEnumerable<string> filePaths)
    {
        Name = name;
        FilePaths = filePaths.ToList();
    }

    public FileExistsValidator(string name, params string[] filePaths)
        : this(name, (IEnumerable<string>)filePaths)
    {
    }

    public Task<ValidationResult> ValidateAsync(
        ValidationContext context, 
        CancellationToken cancellationToken = default)
    {
        var missingFiles = new List<string>();

        foreach (var filePath in FilePaths)
        {
            var fullPath = Path.Combine(context.RepoPath, filePath);
            if (!File.Exists(fullPath))
            {
                missingFiles.Add(filePath);
            }
        }

        if (missingFiles.Count == 0)
        {
            return Task.FromResult(ValidationResult.Pass(Name, 
                $"All {FilePaths.Count} file(s) exist"));
        }

        return Task.FromResult(ValidationResult.Fail(Name,
            $"Missing files: {string.Join(", ", missingFiles)}"));
    }
}
