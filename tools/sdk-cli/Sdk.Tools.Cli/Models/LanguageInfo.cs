namespace Sdk.Tools.Cli.Models;

/// <summary>
/// Information about a detected SDK language.
/// </summary>
/// <param name="Language">The language enum value.</param>
/// <param name="Name">Human-readable language name (e.g., ".NET", "Python").</param>
/// <param name="FileExtension">Canonical file extension including leading period (e.g., ".cs").</param>
public record LanguageInfo(SdkLanguage Language, string Name, string FileExtension);
