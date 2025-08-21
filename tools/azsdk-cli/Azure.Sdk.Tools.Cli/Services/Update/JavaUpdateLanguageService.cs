// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Services;
using System.IO;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Update;

/// <summary>
/// Java-specific update language service. Composes Java repo operations via ILanguageRepoService and
/// implements update semantics for symbol extraction, diff, mapping, and patch proposal.
/// </summary>
public class JavaUpdateLanguageService : UpdateLanguageServiceBase
{
    public JavaUpdateLanguageService(ILanguageRepoService repoService) : base(repoService) { }

    public override Task<Dictionary<string, SymbolInfo>> ExtractSymbolsAsync(string rootPath, CancellationToken ct)
    {
        return Task.FromResult(new Dictionary<string, SymbolInfo>());
    }

    public override Task<List<ApiChange>> DiffAsync(Dictionary<string, SymbolInfo> oldSymbols, Dictionary<string, SymbolInfo> newSymbols)
    {
        return Task.FromResult(new List<ApiChange>());
    }

    public override Task<string?> GetCustomizationRootAsync(UpdateSessionState session, string generationRoot, CancellationToken ct)
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

    public override Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(UpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct)
    {
        // Stub: TODO no impacted files
        return Task.FromResult(new List<CustomizationImpact>());
    }

    public override Task<List<PatchProposal>> ProposePatchesAsync(UpdateSessionState session, IEnumerable<CustomizationImpact> impacts, CancellationToken ct)
    {
    // Stub: create a trivial placeholder patch for each impacted file.
        var proposals = impacts
            .Select(i => new PatchProposal
            {
                File = i.File,
                Diff = $"--- a/{i.File}\n+++ b/{i.File}\n// TODO: computed diff placeholder\n",
                Rationale = "Placeholder: language service proposal"
            })
            .ToList();
        return Task.FromResult(proposals);
    }

    // Prefer marker-based repo root discovery for Java
    protected override string? ResolveValidationPackagePath(UpdateSessionState session)
    {
        var candidates = new[] { session.CustomizationRoot, session.NewGeneratedPath, session.OldGeneratedPath }
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => Directory.Exists(c!) ? c! : (Path.GetDirectoryName(c!) ?? string.Empty))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
        foreach (var start in candidates)
        {
            var found = FindUpwardsWithMarkers(start!, new[] { "pom.xml", "build.gradle", "build.gradle.kts" });
            if (!string.IsNullOrWhiteSpace(found))
            {
                return found;
            }
        }
        return base.ResolveValidationPackagePath(session);
    }

    private static string? FindUpwardsWithMarkers(string startDir, string[] markerFiles)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (markerFiles.Any(m => File.Exists(Path.Combine(dir.FullName, m))))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
