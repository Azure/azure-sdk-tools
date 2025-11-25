// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services.Languages.Samples;

/// <summary>
/// Provides language-specific behaviors and assets for sample generation.
/// </summary>
public abstract class SampleLanguageContext
{
    protected readonly IFileHelper FileHelper;

    protected SampleLanguageContext(IFileHelper fileHelper)
    {
        FileHelper = fileHelper;
    }

    /// <summary>The language identifier (e.g. dotnet, java, typescript, python, go).</summary>
    public abstract string Language { get; }

    /// <summary>The canonical file extension for samples in this language (including leading period).</summary>
    public abstract string FileExtension { get; }

    /// <summary>
    /// Returns the language-specific instructional guidance (excluding the sample example). Implementations provide
    /// only the guidance portion; the base class will append the sample example automatically.
    /// </summary>
    protected abstract string GetLanguageSpecificInstructions();

    /// <summary>Returns a representative sample template ("sample example") for the language.</summary>
    public abstract string GetSampleExample();

    /// <summary>
    /// Returns combined instructions used to steer sample generation (language guidance + example template).
    /// Override if a language needs custom composition behavior.
    /// </summary>
    public virtual string GetSampleGenerationInstructions() =>
        GetLanguageSpecificInstructions() + GetSampleExample();

    /// <summary>
    /// Loads context for sample generation from the specified paths.
    /// </summary>
    /// <param name="paths">Paths to include in the context loading. First path is treated as the package path.</param>
    /// <param name="totalBudget">Maximum aggregate characters for all source files.</param>
    /// <param name="perFileLimit">Maximum characters per individual file before truncation.</param>
    /// <param name="ct">Cancellation token applied to file reads.</param>
    /// <returns>Concatenated structured context string.</returns>
    public virtual async Task<string> LoadContextAsync(IEnumerable<string> paths, int totalBudget, int perFileLimit, CancellationToken ct = default)
    {
        if (!paths.Any())
        {
            throw new ArgumentException("At least one path must be provided", nameof(paths));
        }

        var packagePath = paths.First(); // First path is the package path
        var additionalPaths = paths.Skip(1); // Remaining paths are additional context

        var sourceInputProvider = GetSourceInputProvider();
        var sourceInputs = sourceInputProvider.Create(packagePath).ToList();
        
        // Add additional paths as SourceInput entries
        foreach (var path in additionalPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                var fullPath = Path.GetFullPath(path.Trim());
                sourceInputs.Add(new SourceInput(fullPath, 
                    IncludeExtensions: Array.Empty<string>(),
                    ExcludeGlobPatterns: Array.Empty<string>()));
            }
        }
        
        return await FileHelper.LoadFilesAsync(sourceInputs, packagePath, totalBudget, perFileLimit, f => GetContextAwarePriority(f, packagePath), ct);
    }

    /// <summary>
    /// Priority function that gives much higher priority to files from the main package path
    /// versus files from extra context paths.
    /// </summary>
    /// <param name="f">File metadata.</param>
    /// <param name="packagePath">The main package path.</param>
    /// <returns>Smaller numbers indicate higher priority.</returns>
    protected virtual int GetContextAwarePriority(FileMetadata f, string packagePath)
    {
        var isFromMainPackage = f.FilePath.StartsWith(packagePath, StringComparison.OrdinalIgnoreCase);
        var name = Path.GetFileNameWithoutExtension(f.FilePath);
        var hasClient = name.Contains("client", StringComparison.OrdinalIgnoreCase);
        
        // Files from main package get priority 1-10, extra context get 20-50
        return isFromMainPackage
            ? (hasClient ? 1 : 5)
            : (hasClient ? 20 : 50);
    }

    /// <summary>
    /// Gets the source input provider for this language. Override in derived classes to provide language-specific providers.
    /// </summary>
    protected virtual ILanguageSourceInputProvider GetSourceInputProvider()
    {
        throw new NotImplementedException($"Language '{Language}' must override GetSourceInputProvider() method.");
    }
}
