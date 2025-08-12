// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Update;

/// <summary>
/// Minimal stub implementation for Java update language service so tests compile.
/// Real implementation TODO: invoke TypeSpec Java emitter, parse generated code for symbols, diff, and impact analysis.
/// </summary>
public class JavaUpdateLanguageService : IUpdateLanguageService
{
    public string Language => "java";

    public Task RegenerateAsync(UpdateSessionState session, string specPath, string? newGeneratedPath, CancellationToken ct)
    {
        // Stub: just set a fake generated path if not supplied
        if (string.IsNullOrEmpty(session.NewGeneratedPath))
        {
            session.NewGeneratedPath = newGeneratedPath ?? Path.Combine(Path.GetTempPath(), $"tsp-java-gen-{Guid.NewGuid():N}");
            Directory.CreateDirectory(session.NewGeneratedPath);
        }
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, SymbolInfo>> ExtractSymbolsAsync(string rootPath, CancellationToken ct)
    {
        // Stub: return empty symbol set (no API changes)
        return Task.FromResult(new Dictionary<string, SymbolInfo>());
    }

    public Task<List<ApiChange>> DiffAsync(Dictionary<string, SymbolInfo> oldSymbols, Dictionary<string, SymbolInfo> newSymbols)
    {
        // Stub: no changes
        return Task.FromResult(new List<ApiChange>());
    }

    public Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(UpdateSessionState session, string customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct)
    {
        // Stub: no impacted files
        return Task.FromResult(new List<CustomizationImpact>());
    }
}
