// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
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
    /// <param name="generationRoot">Root folder of newly generated sources (e.g. a <c>src</c> directory).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Absolute path to customization root or <c>null</c> if none is found / applicable.</returns>
    string? GetCustomizationRoot(string generationRoot, CancellationToken ct);

    /// <summary>
    /// Applies automated patches directly to customization code using intelligent analysis.
    /// </summary>
    /// <param name="commitSha">The commit SHA from TypeSpec changes for context</param>
    /// <param name="customizationRoot">Path to the customization root directory</param>
    /// <param name="packagePath">Path to the package directory containing generated code</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if patches were successfully applied; false otherwise</returns>
    Task<bool> ApplyPatchesAsync(string commitSha, string customizationRoot, string packagePath, CancellationToken ct);

    /// <summary>
    /// Performs language-specific validation (build, compile, tests, lint, type-check, etc.).
    /// </summary>
    /// <param name="packagePath">Path to the package directory containing generated code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or a list of validation errors.</returns>
    Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct);
}
