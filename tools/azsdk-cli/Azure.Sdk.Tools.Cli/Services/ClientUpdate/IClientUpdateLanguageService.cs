// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

/// <summary>
/// Contract for a language-specific client update pipeline used by <c>TspClientUpdateTool</c>.
/// Implementations encapsulate extraction of API symbols, diffing, customization impact analysis,
/// patch proposal, validation (build / tests / type checks), and optional auto-fix suggestion.
///
/// Each concrete implementation should be stateless (or at least thread-safe for concurrent calls)
/// and rely on the supplied <see cref="ClientUpdateSessionState"/> for per-run mutable data.
/// </summary>
public interface IClientUpdateLanguageService
{
    /// <summary>
    /// Produces an API change list by diffing file contents between two generated source trees.
    /// Implementations may perform a structural or textual diff; when <paramref name="oldGenerationPath"/> is null
    /// they should treat the operation as an initial generation (returning an empty change list).
    /// </summary>
    /// <param name="oldGenerationPath">Previous generation</param>
    /// <param name="newGenerationPath">New/current generation root.</param>
    /// <returns>List of detected API changes (empty if no differences).</returns>
    Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath);

    /// <summary>
    /// Locates the customization (hand-authored) root directory for the language, if any.
    /// </summary>
    /// <param name="session">Current update session state.</param>
    /// <param name="generationRoot">Root folder of newly generated sources (e.g. a <c>src</c> directory).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Absolute path to customization root or <c>null</c> if none is found / applicable.</returns>
    string? GetCustomizationRootAsync(ClientUpdateSessionState session, string generationRoot, CancellationToken ct);

    /// <summary>
    /// Analyzes which customization files are impacted by the supplied API changes.
    /// </summary>
    /// <param name="session">Current update session.</param>
    /// <param name="customizationRoot">Customization root (may be <c>null</c> for languages without customizations).</param>
    /// <param name="apiChanges">Sequence of API changes from <see cref="DiffAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of impacted customization descriptors (empty if none).</returns>
    Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(ClientUpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct);

    /// <summary>
    /// Proposes patch diffs for impacted customization files.
    /// </summary>
    /// <param name="session">Current session.</param>
    /// <param name="impacts">Impacted customization items.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Patch proposals (empty if no modifications recommended).</returns>
    Task<List<PatchProposal>> ProposePatchesAsync(ClientUpdateSessionState session, IEnumerable<CustomizationImpact> impacts, CancellationToken ct);

    /// <summary>
    /// Performs language-specific validation (build, compile, tests, lint, type-check, etc.).
    /// </summary>
    /// <param name="session">Current session (contains locations for old/new generated outputs).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or a list of validation errors.</returns>
    Task<ValidationResult> ValidateAsync(ClientUpdateSessionState session, CancellationToken ct);

    /// <summary>
    /// Optionally proposes conservative patches that address validation failures (e.g. formatting, minor import fixes).
    /// Implementations may return an empty list if no automatic fixes are available or safe.
    /// </summary>
    /// <param name="session">Current update session.</param>
    /// <param name="validationErrors">Errors from the previous <see cref="ValidateAsync"/> invocation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of patch proposals representing potential fixes (may be empty).</returns>
    Task<List<PatchProposal>> ProposeFixesAsync(ClientUpdateSessionState session, List<string> validationErrors, CancellationToken ct);
}
