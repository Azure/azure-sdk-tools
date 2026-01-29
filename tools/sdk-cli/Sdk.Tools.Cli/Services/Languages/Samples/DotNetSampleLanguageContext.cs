// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using ApiExtractor.DotNet;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages.Samples;

public sealed class DotNetSampleLanguageContext : SampleLanguageContext
{
    private readonly CSharpApiExtractor _extractor = new();
    
    public DotNetSampleLanguageContext(FileHelper fileHelper) : base(fileHelper) { }

    public override SdkLanguage Language => SdkLanguage.DotNet;

    protected override string[] DefaultIncludeExtensions => [".cs"];

    protected override string[] DefaultExcludePatterns => 
    [ 
        "**/obj/**", 
        "**/bin/**", 
        "**/*.Designer.cs",
        "**/AssemblyInfo.cs"
    ];

    /// <summary>
    /// Streams API surface context using static analysis instead of raw source.
    /// This provides ~95% reduction in context size while preserving all public API information.
    /// </summary>
    public override async IAsyncEnumerable<string> StreamContextAsync(
        IEnumerable<string> paths,
        SdkCliConfig? config = null,
        int totalBudget = SampleConstants.MaxContextCharacters,
        int perFileLimit = SampleConstants.MaxCharactersPerFile,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!paths.Any())
            throw new ArgumentException("At least one path must be provided", nameof(paths));

        // Find the source directory from paths
        var sourcePath = paths
            .Select(p => Path.GetFullPath(p.Trim()))
            .FirstOrDefault(Directory.Exists);
        
        if (sourcePath == null)
        {
            // Fall back to base implementation for individual files
            await foreach (var chunk in base.StreamContextAsync(paths, config, totalBudget, perFileLimit, ct))
                yield return chunk;
            yield break;
        }

        // Extract API surface using Roslyn static analysis
        var apiIndex = await _extractor.ExtractAsync(sourcePath, ct);
        
        // Format as human-readable C# syntax (more readable than JSON for LLMs)
        var apiSurface = CSharpFormatter.Format(apiIndex);
        
        // Yield the API surface wrapped in XML tag for clear structure
        yield return $"<api-surface package=\"{apiIndex.Package}\">\n";
        yield return apiSurface;
        yield return "</api-surface>\n";
    }

    protected override int GetPriority(FileMetadata file)
    {
        var path = file.RelativePath.Replace('\\', '/').ToLowerInvariant();
        var name = Path.GetFileNameWithoutExtension(file.FilePath).ToLowerInvariant();
        
        // Deprioritize generated code - load human-written code first
        var isGenerated = path.Contains("/generated/") || 
                          name.EndsWith(".g") ||
                          name.Contains("generated");
        var basePriority = isGenerated ? 100 : 0;
        
        // Within each category, prioritize key files
        if (name.Contains("client")) return basePriority + 1;
        if (name.Contains("options")) return basePriority + 2;
        if (name.Contains("model")) return basePriority + 3;
        return basePriority + 10;
    }

    public override string GetInstructions() =>
        "C#: file-scoped namespaces, var, async/await, using statements, try/catch, .NET 8+.";
}
