using Azure.Sdk.Tools.Cli.Models;
namespace Azure.Sdk.Tools.Cli.Services.Update;

public interface IUpdateLanguageService
{
    /// <summary>
    /// Short language key this service supports (e.g. "java", "csharp"). Used to resolve the strategy.
    /// </summary>
    string Language { get; }

    /// <summary>
    /// Regenerates (or triggers generation of) fresh SDK code for the given spec into a staging path.
    /// Implementations should:
    /// 1. Invoke the appropriate TypeSpec emitter or generator for the language.
    /// 2. Write all newly generated artifacts under the provided or computed <paramref name="newGeneratedPath"/>.
    /// 3. Record the final path on <see cref="UpdateSessionState.NewGeneratedPath"/>.
    /// 4. Avoid mutating any previous generation (the caller snapshots old output separately).
    /// </summary>
    Task RegenerateAsync(UpdateSessionState session, string specPath, string? newGeneratedPath, CancellationToken ct);

    /// <summary>
    /// Performs lightweight symbol extraction over a generated code tree and returns a dictionary of symbols keyed by a stable identifier.
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
    /// Analyzes which customization files are impacted by the supplied API changes. Implementations scan the customization root
    /// (developer-authored extensions / overrides) and return files along with textual reasons (e.g. symbol reference matches).
    /// This powers the mapping stage: impacted files will later receive patch proposals, while non-impacted may be directly merged.
    /// </summary>
    Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(UpdateSessionState session, string customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct);
}
