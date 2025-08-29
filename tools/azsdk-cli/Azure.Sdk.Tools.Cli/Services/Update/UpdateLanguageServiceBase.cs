// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Update;

/// <summary>
/// Base class for language update services that composes a language-specific ILanguageRepoService.
/// Implements common validation by delegating to the repo service, while leaving update semantics abstract.
/// </summary>
public abstract class UpdateLanguageServiceBase : IUpdateLanguageService
{
    protected ILanguageSpecificCheckResolver languageSpecificCheckResolver { get; }

    protected UpdateLanguageServiceBase(ILanguageSpecificCheckResolver languageSpecificCheckResolver)
    {
        this.languageSpecificCheckResolver = languageSpecificCheckResolver;
    }

    public abstract Task<Dictionary<string, SymbolInfo>> ExtractSymbolsAsync(string rootPath, CancellationToken ct);
    public abstract Task<List<ApiChange>> DiffAsync(Dictionary<string, SymbolInfo> oldSymbols, Dictionary<string, SymbolInfo> newSymbols);
    public abstract Task<string?> GetCustomizationRootAsync(UpdateSessionState session, string generationRoot, CancellationToken ct);
    public abstract Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(UpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct);
    public abstract Task<List<PatchProposal>> ProposePatchesAsync(UpdateSessionState session, IEnumerable<CustomizationImpact> impacts, CancellationToken ct);

    /// <summary>
    /// Default validation delegates to the repo service test run using the resolved package path.
    /// Languages can override to run lint/format/type-check as needed.
    /// </summary>
    public virtual async Task<ValidationResult> ValidateAsync(UpdateSessionState session, CancellationToken ct)
    {
        string? packagePath = session.OldGeneratedPath;    

        if (string.IsNullOrWhiteSpace(packagePath) || !Directory.Exists(packagePath))
        {
            // If we cannot resolve a package path, don't hard-fail; treat as success to avoid blocking.
            return ValidationResult.CreateSuccess();
        }
        // NOTE: We intentionally do NOT depend on a RunTestsAsync method yet. Until the
        // language-specific validation surface is expanded. When real build/test/lint hooks are
        // added to ILanguageSpecificChecks, replace this with a composite validation.

        var checks = await languageSpecificCheckResolver.GetLanguageCheckAsync(packagePath);
        if (checks == null)
        {
            return ValidationResult.CreateSuccess(); // No checks available → do not block.
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

    /// <summary>
    /// Default: no automatic fixes available. Languages may override to propose conservative fixes
    /// (formatters, import/fixers, small shims) based on validation errors.
    /// </summary>
    public virtual Task<List<PatchProposal>> ProposeFixesAsync(UpdateSessionState session, List<string> validationErrors, CancellationToken ct)
    {
        return Task.FromResult(new List<PatchProposal>());
    }
}
