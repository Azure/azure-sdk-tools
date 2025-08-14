using Azure.Sdk.Tools.Cli.Models;
namespace Azure.Sdk.Tools.Cli.Services.Update;

public interface IUpdateLanguageService
{
    /// <summary>
    /// Short language key this service supports (e.g. "java", "csharp"). Used to resolve the strategy.
    /// </summary>
    string Language { get; }

    /// <summary>
    /// Performs symbol extraction over a generated code tree and returns a dictionary of symbols keyed by a stable identifier.
    /// Symbols represent public API surface elements (classes, methods, etc.) relevant for diffing. Each <see cref="SymbolInfo"/> should capture:
    ///  - Id: unique key (e.g. ClassName or MethodName:Signature)
    ///  - Kind: classification (e.g. class, method)
    ///  - Signature (for callable members) sufficient to detect breaking changes.
    /// Implementations may use parsing, regex heuristics, or AST tooling depending on language maturity.
    /// </summary>
    Task<Dictionary<string, SymbolInfo>> ExtractSymbolsAsync(string rootPath, CancellationToken ct);

    /// <summary>
    /// Produces an API change list by comparing old vs new symbol dictionaries. Implementations should populate rich change fields
    /// (old/new signatures, parameter differences, breaking flag) when available. Minimal implementations may still only set Kind, Symbol, Detail.
    /// </summary>
    Task<List<ApiChange>> DiffAsync(Dictionary<string, SymbolInfo> oldSymbols, Dictionary<string, SymbolInfo> newSymbols);

    /// <summary>
    /// Discover the (single) customization root directory for this language given the generation root.
    /// Return null if none exists. Some languages (e.g., Java) centralize customizations in one location.
    /// </summary>
    Task<string?> GetCustomizationRootAsync(UpdateSessionState session, string generationRoot, CancellationToken ct);

    /// <summary>
    /// Analyze which customization files (under the single customization root) are impacted by the supplied API changes.
    /// Return a list of impacted files with reasons; empty list if none or if root is null.
    /// </summary>
    Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(UpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct);

    /// <summary>
    /// Determine which customization files can be directly merged (no local edits conflicting) by comparing regenerated vs customization versions.
    /// Return list of file paths eligible for direct merge.
    /// </summary>
    Task<List<string>> DetectDirectMergeFilesAsync(UpdateSessionState session, string? customizationRoot, CancellationToken ct);

    /// <summary>
    /// Produce proposed patch diffs for the impacted customization files not directly merged.
    /// </summary>
    Task<List<PatchProposal>> ProposePatchesAsync(UpdateSessionState session, IEnumerable<CustomizationImpact> impacts, IEnumerable<string> directMergeFiles, CancellationToken ct);

    /// <summary>
    /// Perform language-specific build / type / syntax validation (e.g., compile, mypy, tsc). Return tuple (success, errors).
    /// </summary>
    Task<(bool success, List<string> errors)> ValidateAsync(UpdateSessionState session, CancellationToken ct);
}
