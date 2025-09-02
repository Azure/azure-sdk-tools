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
    Task<IClientUpdateLanguageService?> ResolveAsync(string? packagePath, CancellationToken ct = default);
}

public class ClientUpdateLanguageServiceResolver : IClientUpdateLanguageServiceResolver
{
    private readonly IEnumerable<IClientUpdateLanguageService> _services;
    private readonly ILanguageSpecificCheckResolver _languageCheckResolver;
    private readonly ILogger<ClientUpdateLanguageServiceResolver> _logger;

    public ClientUpdateLanguageServiceResolver(IEnumerable<IClientUpdateLanguageService> services,
        ILanguageSpecificCheckResolver languageCheckResolver,
        ILogger<ClientUpdateLanguageServiceResolver> logger)
    {
        _services = services;
        _languageCheckResolver = languageCheckResolver;
        _logger = logger;
    }

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
