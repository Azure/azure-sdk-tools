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
    /// <summary>
    /// Resolves language-specific dependency / quality checks for a generated client package.
    /// Implementations typically supply an instance able to run build / type / dependency validations.
    /// </summary>
    protected ILanguageSpecificResolver<ILanguageSpecificChecks> LanguageSpecificChecks { get; }

    /// <summary>
    /// Initializes the base language service.
    /// </summary>
    /// <param name="languageSpecificChecks">Resolver that returns an object capable of executing validation checks for a given generated package path.</param>
    protected ClientUpdateLanguageServiceBase(ILanguageSpecificResolver<ILanguageSpecificChecks> languageSpecificChecks)
    {
        this.LanguageSpecificChecks = languageSpecificChecks;
    }

    /// <summary>
    /// Produces a semantic API change list between an older generated package and a newly generated package.
    /// </summary>
    /// <param name="oldGenerationPath">File-system path to the previously generated client code ("old" baseline).</param>
    /// <param name="newGenerationPath">File-system path to the newly generated client code ("new" candidate).</param>
    /// <returns>A list of <see cref="ApiChange"/> instances describing added / removed / modified surface area. Empty list means no detectable API changes.</returns>
    /// <remarks>Implementations are responsible for any parsing / compilation needed to compute differences.</remarks>
    public abstract Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath);
    /// <summary>
    /// Locates the root directory containing user customizations that extend or wrap the generated code for the given session.
    /// </summary>
    /// <param name="session">Active update session state (contains generation paths and identifiers).</param>
    /// <param name="generationRoot">Root path of the generated client currently under consideration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The absolute path to the customization root directory, or <c>null</c> if no customization area exists / is required.</returns>
    public abstract string? GetCustomizationRootAsync(ClientUpdateSessionState session, string generationRoot, CancellationToken ct);
    
    /// <summary>
    /// Applies LLM-based patches directly to customization code using AI analysis.
    /// </summary>
    /// <param name="commitSha">The commit SHA from TypeSpec changes for context</param>
    /// <param name="customizationRoot">Path to the customization root directory</param>
    /// <param name="newGeneratedPath">Path to the newly generated code (current packagePath)</param>
    /// <param name="oldGeneratedPath">Path to the old generated code (backup directory)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if patches were successfully applied; false otherwise</returns>
    public abstract Task<bool> ApplyLlmPatchesAsync(string commitSha, string customizationRoot, string newGeneratedPath, string oldGeneratedPath, CancellationToken ct);

    /// <summary>
    /// Performs language-specific generation validation (typically dependency / build / type checks) against <see cref="ClientUpdateSessionState.NewGeneratedPath"/>.
    /// </summary>
    /// <param name="session">Active update session containing the path of the newly generated client code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="ValidationResult"/> indicating success if validation passes or the language has no checks registered.
    /// On failure, contains one or more human-readable error messages describing the root cause (e.g., dependency conflicts).
    /// </returns>
    /// <remarks>Default implementation runs <c>AnalyzeDependenciesAsync</c> if a resolver returns checks; implementations may override for richer validation.</remarks>
    public virtual async Task<ValidationResult> ValidateAsync(ClientUpdateSessionState session, CancellationToken ct)
    {
        string? packagePath = session.NewGeneratedPath;
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return ValidationResult.CreateFailure("Package path not specified (session.NewGeneratedPath is empty)");
        }
        if (!Directory.Exists(packagePath))
        {
            return ValidationResult.CreateFailure($"Package path not found: {packagePath}");
        }
        var checks = await LanguageSpecificChecks.Resolve(packagePath);
        if (checks == null)
        {
            return ValidationResult.CreateSuccess();
        }
        try
        {
            var depResult = await checks.AnalyzeDependenciesAsync(packagePath, false, ct);
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


}
