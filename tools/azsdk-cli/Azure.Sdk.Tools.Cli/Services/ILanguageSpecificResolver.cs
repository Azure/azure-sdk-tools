namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Provides a mechanism for resolving services that have language-specific implementations based on a package path.
/// </summary>
public interface ILanguageSpecificResolver<T> where T : class
{
    /// <summary>
    /// Resolves a language-specific service implementation based on the provided package path.
    /// </summary>
    /// <typeparam name="TService">The type of the service to resolve.</typeparam>
    /// <param name="packagePath">The path to the package for which to resolve the service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved service instance, or null if no matching service is found.</returns>
    public Task<T?> Resolve(string packagePath, CancellationToken ct = default);
}