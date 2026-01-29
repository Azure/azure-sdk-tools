// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Text;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages.Samples;

/// <summary>
/// Provides language-specific behaviors for sample generation.
/// All folder structure is config-driven, not hardcoded.
/// </summary>
public abstract class SampleLanguageContext
{
    protected readonly FileHelper FileHelper;

    protected SampleLanguageContext(FileHelper fileHelper)
    {
        FileHelper = fileHelper;
    }

    /// <summary>The language enum value.</summary>
    public abstract SdkLanguage Language { get; }

    /// <summary>The canonical file extension for samples (including leading period).</summary>
    public virtual string FileExtension => SdkLanguageHelpers.GetFileExtension(Language);

    /// <summary>
    /// Returns language-specific coding conventions for sample generation.
    /// </summary>
    public abstract string GetInstructions();

    /// <summary>
    /// Default include extensions for this language.
    /// </summary>
    protected abstract string[] DefaultIncludeExtensions { get; }

    /// <summary>
    /// Default exclude patterns for this language.
    /// </summary>
    protected abstract string[] DefaultExcludePatterns { get; }

    /// <summary>
    /// Streams context for sample generation without materializing in memory.
    /// Preferred over LoadContextAsync for large contexts.
    /// </summary>
    public virtual async IAsyncEnumerable<string> StreamContextAsync(
        IEnumerable<string> paths, 
        SdkCliConfig? config = null,
        int totalBudget = SampleConstants.MaxContextCharacters,
        int perFileLimit = SampleConstants.MaxCharactersPerFile,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!paths.Any())
            throw new ArgumentException("At least one path must be provided", nameof(paths));

        // Build source inputs from paths
        var includeExtensions = config?.IncludePatterns is { Length: > 0 } 
            ? ExtractExtensions(config.IncludePatterns) 
            : DefaultIncludeExtensions;
        var excludePatterns = config?.ExcludePatterns ?? DefaultExcludePatterns;

        List<SourceInputSpec> sourceInputs = [];
        foreach (var path in paths)
        {
            var fullPath = Path.GetFullPath(path.Trim());
            if (Directory.Exists(fullPath))
            {
                sourceInputs.Add(new SourceInputSpec(fullPath, includeExtensions, excludePatterns));
            }
            else if (File.Exists(fullPath))
            {
                sourceInputs.Add(new SourceInputSpec(fullPath));
            }
        }

        if (sourceInputs is [])
            throw new ArgumentException("No valid paths found", nameof(paths));

        var basePath = Path.GetFullPath(paths.First());
        
        // Create a single group with the specified budget
        var group = new SourceInputGroup(
            SectionName: "source-code",
            Inputs: sourceInputs,
            Budget: totalBudget,
            PerFileLimit: perFileLimit,
            PriorityFunc: f => GetPriority(f)
        );
        
        // Stream content directly without materialization
        await foreach (var chunk in FileHelper.StreamFilesAsync([group], basePath, ct))
        {
            yield return chunk.Content;
        }
    }

    /// <summary>
    /// Loads context for sample generation from the specified paths.
    /// Uses config overrides if provided, otherwise falls back to language defaults.
    /// Note: Prefer StreamContextAsync to avoid materializing large contexts in memory.
    /// </summary>
    public virtual async Task<string> LoadContextAsync(
        IEnumerable<string> paths, 
        SdkCliConfig? config = null,
        int totalBudget = SampleConstants.MaxContextCharacters,
        int perFileLimit = SampleConstants.MaxCharactersPerFile,
        CancellationToken ct = default)
    {
        var contextBuilder = new StringBuilder();
        await foreach (var chunk in StreamContextAsync(paths, config, totalBudget, perFileLimit, ct))
        {
            contextBuilder.Append(chunk);
        }
        return contextBuilder.ToString();
    }

    /// <summary>
    /// Priority function - override in subclass for language-specific prioritization.
    /// Lower number = higher priority.
    /// </summary>
    protected virtual int GetPriority(FileMetadata file)
    {
        // Default: all files equal priority
        return 10;
    }

    private static string[] ExtractExtensions(string[] patterns)
    {
        // Extract extensions from patterns like "**/*.cs" -> ".cs"
        return patterns
            .Where(p => p.Contains("*."))
            .Select(p => "." + p.Split("*.").Last().TrimEnd('*', '/'))
            .Distinct()
            .ToArray();
    }
}
