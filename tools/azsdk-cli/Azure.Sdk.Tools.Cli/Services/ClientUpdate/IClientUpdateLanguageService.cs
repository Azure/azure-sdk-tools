// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

public interface IClientUpdateLanguageService
{
    // Extract public API symbols from a generated code tree
    Task<Dictionary<string, SymbolInfo>> ExtractSymbolsAsync(string rootPath, CancellationToken ct);

    // Diff two symbol sets into API changes
    Task<List<ApiChange>> DiffAsync(Dictionary<string, SymbolInfo> oldSymbols, Dictionary<string, SymbolInfo> newSymbols);

    // Locate customization root (if any) for this language
    Task<string?> GetCustomizationRootAsync(ClientUpdateSessionState session, string generationRoot, CancellationToken ct);

    // Analyze which customization files are impacted by API changes. For single-file languages,
    // typically return one impact pointing to the canonical customization file.
    Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(ClientUpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct);

    // Propose patches for impacted files, aggregates changes into one patch
    Task<List<PatchProposal>> ProposePatchesAsync(ClientUpdateSessionState session, IEnumerable<CustomizationImpact> impacts, CancellationToken ct);

    // Perform language-specific validation (build/tests/type checks)
    Task<ValidationResult> ValidateAsync(ClientUpdateSessionState session, CancellationToken ct);

    // Optionally propose conservative fixes targeted at validation failures (formatting, import fixes, small shims).
    // Default implementations may return an empty list when no auto-fixes are available.
    Task<List<PatchProposal>> ProposeFixesAsync(ClientUpdateSessionState session, List<string> validationErrors, CancellationToken ct);
}
