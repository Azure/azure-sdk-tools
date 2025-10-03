using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// A service that provides functionality specific to a single SDK language.
/// </summary>
public interface ILanguageSpecificService
{
    /// <summary>
    /// The language that this service implementation supports.
    /// </summary>
    SdkLanguage SupportedLanguage { get; }
}