// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.SampleGeneration;

/// <summary>
/// Provides language-specific behaviors and assets for sample generation.
/// </summary>
public interface ISampleLanguageContext
{
    /// <summary>The language identifier (e.g. dotnet, java, typescript, python, go).</summary>
    string Language { get; }
    /// <summary>The canonical file extension for samples in this language (including leading period).</summary>
    string FileExtension { get; }
    /// <summary>Returns language-specific instructional guidance used to steer sample generation.</summary>
    string GetSampleGenerationInstructions();
    /// <summary>Returns a representative sample template ("sample example") for the language.</summary>
    string GetSampleExample();
    /// <summary>
    /// Loads source code context for the client library to aid sample generation.
    /// </summary>
    /// <param name="packagePath">Root path to the language-specific package.</param>
    /// <param name="totalBudget">Maximum aggregate characters for all source files.</param>
    /// <param name="perFileLimit">Maximum characters per individual file before truncation.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="ct">Cancellation token applied to file reads.</param>
    /// <returns>Concatenated structured source context string.</returns>
    Task<string> GetClientLibrarySourceCodeAsync(string packagePath, int totalBudget, int perFileLimit, ILogger? logger = null, CancellationToken ct = default);
}
