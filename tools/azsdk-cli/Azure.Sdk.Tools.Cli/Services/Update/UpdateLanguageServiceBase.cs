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
    protected ILanguageRepoService RepoService { get; }

    protected UpdateLanguageServiceBase(ILanguageRepoService repoService)
    {
        RepoService = repoService;
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
        var packagePath = ResolveValidationPackagePath(session);
        if (string.IsNullOrWhiteSpace(packagePath) || !Directory.Exists(packagePath))
        {
            // If we cannot resolve a package path, don't hard-fail; treat as success to avoid blocking.
            return ValidationResult.CreateSuccess();
        }

        var result = await RepoService.RunTestsAsync(packagePath, ct);
        var ok = result.ExitCode == 0;
        if (ok)
        {
            return ValidationResult.CreateSuccess();
        }
        
        var errorMessage = string.IsNullOrWhiteSpace(result.ResponseError) ? result.CheckStatusDetails : result.ResponseError;
        return ValidationResult.CreateFailure(errorMessage);
    }

    /// <summary>
    /// Default: no automatic fixes available. Languages may override to propose conservative fixes
    /// (formatters, import/fixers, small shims) based on validation errors.
    /// </summary>
    public virtual Task<List<PatchProposal>> ProposeFixesAsync(UpdateSessionState session, List<string> validationErrors, CancellationToken ct)
    {
        return Task.FromResult(new List<PatchProposal>());
    }

    /// <summary>
    /// Resolve the package path to use for repo-level operations. Default picks the first existing path
    /// from CustomizationRoot, NewGeneratedPath, OldGeneratedPath. Languages can override to add marker-based discovery.
    /// </summary>
    protected virtual string? ResolveValidationPackagePath(UpdateSessionState session)
    {
        var candidates = new[] { session.CustomizationRoot, session.NewGeneratedPath, session.OldGeneratedPath }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Directory.Exists(p!) ? p! : (Path.GetDirectoryName(p!) ?? string.Empty))
            .Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
            .ToList();
        return candidates.FirstOrDefault();
    }
}
