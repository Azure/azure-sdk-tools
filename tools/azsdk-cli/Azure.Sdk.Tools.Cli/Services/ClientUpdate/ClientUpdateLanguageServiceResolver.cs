// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Services; // for ILanguageSpecificCheckResolver

namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

/// <summary>
/// Resolves an <see cref="IClientUpdateLanguageService"/> implementation based on detected package language.
/// Reuses existing language detection logic via ILanguageSpecificCheckResolver + individual service SupportedLanguage values.
/// </summary>
public interface IClientUpdateLanguageServiceResolver
{
    /// <summary>
    /// Attempts to resolve an <see cref="IClientUpdateLanguageService"/> for the supplied generated package path.
    /// </summary>
    /// <param name="packagePath">Root directory of a generated SDK package whose language needs to be inferred. May be <c>null</c> or non-existent.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Matching language service when the language can be determined and a service is registered;
    /// otherwise the first registered service (best-effort fallback), or <c>null</c> if none are registered.
    /// </returns>
    Task<IClientUpdateLanguageService?> ResolveAsync(string? packagePath, CancellationToken ct = default);
}

/// <summary>
/// Default implementation of <see cref="IClientUpdateLanguageServiceResolver"/> which uses the shared
/// <see cref="ILanguageSpecificCheckResolver"/> to detect a package's language, then matches the language
/// against registered <see cref="IClientUpdateLanguageService.SupportedLanguage"/> values. Provides defensive
/// fallbacks (first registered service) when detection fails or an exact service is not available.
/// </summary>
public class ClientUpdateLanguageServiceResolver : IClientUpdateLanguageServiceResolver
{
    private readonly IEnumerable<IClientUpdateLanguageService> _services;
    private readonly ILanguageSpecificCheckResolver _languageCheckResolver;
    private readonly ILogger<ClientUpdateLanguageServiceResolver> _logger;

    /// <summary>
    /// Creates a new resolver instance.
    /// </summary>
    /// <param name="services">All registered client update language services (at least one recommended).</param>
    /// <param name="languageCheckResolver">Resolver used to infer the language from a generated package path.</param>
    /// <param name="logger">Logger for diagnostics and fallback reporting.</param>
    public ClientUpdateLanguageServiceResolver(IEnumerable<IClientUpdateLanguageService> services,
        ILanguageSpecificCheckResolver languageCheckResolver,
        ILogger<ClientUpdateLanguageServiceResolver> logger)
    {
        _services = services;
        _languageCheckResolver = languageCheckResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IClientUpdateLanguageService?> ResolveAsync(string? packagePath, CancellationToken ct = default)
    {
        // Fallback if path missing
        if (string.IsNullOrWhiteSpace(packagePath) || !Directory.Exists(packagePath))
        {
            _logger.LogDebug("Package path not provided or missing; using first registered client update language service.");
            return _services.FirstOrDefault();
        }

        try
        {
            var langCheck = await _languageCheckResolver.GetLanguageCheckAsync(packagePath);
            if (langCheck == null)
            {
                _logger.LogWarning("Could not detect language for {PackagePath}; using first registered service.", packagePath);
                return _services.FirstOrDefault();
            }
            var language = langCheck.SupportedLanguage;
            var match = _services.FirstOrDefault(s => string.Equals(s.SupportedLanguage, language, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                _logger.LogWarning("No client update language service found for '{Language}'; using first registered service.", language);
                return _services.FirstOrDefault();
            }
            _logger.LogInformation("Resolved client update language service {Service} for language {Language}.", match.GetType().Name, language);
            return match;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving client update language service for path {PackagePath}", packagePath);
            return _services.FirstOrDefault();
        }
    }
}
