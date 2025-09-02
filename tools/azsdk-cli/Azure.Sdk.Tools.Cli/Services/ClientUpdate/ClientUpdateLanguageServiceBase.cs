// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

/// <summary>
/// Base class for client update language services that compose language-specific checks.
/// Provides default validation and no-op autofix behavior; concrete language services override
/// extraction, diffing, impact analysis, patch proposal, and optional fix proposal.
/// </summary>
public abstract class ClientUpdateLanguageServiceBase : IClientUpdateLanguageService
{
    protected ILanguageSpecificCheckResolver languageSpecificCheckResolver { get; }

    protected ClientUpdateLanguageServiceBase(ILanguageSpecificCheckResolver languageSpecificCheckResolver)
    {
        this.languageSpecificCheckResolver = languageSpecificCheckResolver;
    }

    public abstract Task<Dictionary<string, SymbolInfo>> ExtractSymbolsAsync(string rootPath, CancellationToken ct);
    public abstract Task<List<ApiChange>> DiffAsync(Dictionary<string, SymbolInfo> oldSymbols, Dictionary<string, SymbolInfo> newSymbols);
    public abstract Task<string?> GetCustomizationRootAsync(ClientUpdateSessionState session, string generationRoot, CancellationToken ct);
    public abstract Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(ClientUpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct);
    public abstract Task<List<PatchProposal>> ProposePatchesAsync(ClientUpdateSessionState session, IEnumerable<CustomizationImpact> impacts, CancellationToken ct);

    public virtual async Task<ValidationResult> ValidateAsync(ClientUpdateSessionState session, CancellationToken ct)
    {
        string? packagePath = session.NewGeneratedPath;
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return ValidationResult.CreateFailure("Package path not specified (session.OldGeneratedPath is empty)");
        }
        if (!Directory.Exists(packagePath))
        {
            return ValidationResult.CreateFailure($"Package path not found: {packagePath}");
        }
        var checks = await languageSpecificCheckResolver.GetLanguageCheckAsync(packagePath);
        if (checks == null)
        {
            return ValidationResult.CreateSuccess();
        }
        try
        {
            var depResult = await checks.AnalyzeDependenciesAsync(packagePath, ct);
            if (depResult.ExitCode == 0)
            {
                return ValidationResult.CreateSuccess();
            }
            var errorMessage = string.IsNullOrWhiteSpace(depResult.ResponseError) ? depResult.CheckStatusDetails : depResult.ResponseError;
            return ValidationResult.CreateFailure(errorMessage ?? "Validation failed");
        }
        catch (Exception ex)
        {
            return ValidationResult.CreateFailure($"Validation exception: {ex.Message}");
        }
    }

    public virtual Task<List<PatchProposal>> ProposeFixesAsync(ClientUpdateSessionState session, List<string> validationErrors, CancellationToken ct)
    {
        return Task.FromResult(new List<PatchProposal>());
    }
}
