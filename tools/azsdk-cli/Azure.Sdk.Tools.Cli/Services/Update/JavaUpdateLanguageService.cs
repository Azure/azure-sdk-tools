// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Update;

/// <summary>
/// Minimal stub implementation for Java update language service so tests compile.
/// Real implementation TODO: parse generated code for symbols, diff, and impact analysis.
/// </summary>
public class JavaUpdateLanguageService : IUpdateLanguageService
{
    public string Language => "java";

    public Task<Dictionary<string, SymbolInfo>> ExtractSymbolsAsync(string rootPath, CancellationToken ct)
    {
        return Task.FromResult(new Dictionary<string, SymbolInfo>());
    }

    public Task<List<ApiChange>> DiffAsync(Dictionary<string, SymbolInfo> oldSymbols, Dictionary<string, SymbolInfo> newSymbols)
    {
        return Task.FromResult(new List<ApiChange>());
    }

    public Task<string?> GetCustomizationRootAsync(UpdateSessionState session, string generationRoot, CancellationToken ct)
    {
        try
        {
            // In azure-sdk-for-java layout, generated code lives under:
            //   <pkgRoot>/azure-<package>-<service>/src
            // Customizations (single root) live under parallel directory:
            //   <pkgRoot>/azure-<package>-<service>/customization/src/main/java
            // Example (document intelligence):
            //   generated root: .../azure-ai-documentintelligence/src
            //   customization root: .../azure-ai-documentintelligence/customization/src/main/java

            var packageRoot = Directory.GetParent(generationRoot)?.FullName; // parent of 'src'
            if (!string.IsNullOrEmpty(packageRoot) && Directory.Exists(packageRoot))
            {
                var customizationDir = Path.Combine(packageRoot, "customization");
                var customizationSourceRoot = Path.Combine(customizationDir, "src", "main", "java");
                if (Directory.Exists(customizationSourceRoot))
                {
                    return Task.FromResult<string?>(customizationSourceRoot);
                }
            }
        }
        catch { /* swallow heuristic errors */ }
        return Task.FromResult<string?>(null);
    }

    public Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(UpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct)
    {
        // Stub: TODO no impacted files
        return Task.FromResult(new List<CustomizationImpact>());
    }

    public Task<List<string>> DetectDirectMergeFilesAsync(UpdateSessionState session, string? customizationRoot, CancellationToken ct)
    {
        // Stub: no direct merges detected yet.
        return Task.FromResult(new List<string>());
    }

    public Task<List<PatchProposal>> ProposePatchesAsync(UpdateSessionState session, IEnumerable<CustomizationImpact> impacts, IEnumerable<string> directMergeFiles, CancellationToken ct)
    {
        // Stub: create a trivial placeholder patch for each impacted file not directly merged.
        var proposals = impacts
            .Where(i => !directMergeFiles.Contains(i.File, StringComparer.OrdinalIgnoreCase))
            .Select(i => new PatchProposal
            {
                File = i.File,
                Diff = $"--- a/{i.File}\n+++ b/{i.File}\n// TODO: computed diff placeholder\n",
                Rationale = "Placeholder: language service proposal"
            })
            .ToList();
        return Task.FromResult(proposals);
    }

    public Task<(bool success, List<string> errors)> ValidateAsync(UpdateSessionState session, CancellationToken ct)
    {
        // Stub: pretend validation passes.
        return Task.FromResult((true, new List<string>()));
    }
}
