// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.SampleGeneration;

/// <summary>
/// Provides language-specific behaviors and assets for sample generation.
/// </summary>
public abstract class SampleLanguageContext
{
    protected readonly IFileHelper FileHelper;

    protected SampleLanguageContext(IFileHelper fileHelper)
    {
        FileHelper = fileHelper;
    }

    /// <summary>The language identifier (e.g. dotnet, java, typescript, python, go).</summary>
    public abstract string Language { get; }

    /// <summary>The canonical file extension for samples in this language (including leading period).</summary>
    public abstract string FileExtension { get; }

    /// <summary>
    /// Returns the language-specific instructional guidance (excluding the sample example). Implementations provide
    /// only the guidance portion; the base class will append the sample example automatically.
    /// </summary>
    protected abstract string GetLanguageSpecificInstructions();

    /// <summary>Returns a representative sample template ("sample example") for the language.</summary>
    public abstract string GetSampleExample();

    /// <summary>
    /// Returns combined instructions used to steer sample generation (language guidance + example template).
    /// Override if a language needs custom composition behavior.
    /// </summary>
    public virtual string GetSampleGenerationInstructions() =>
        GetLanguageSpecificInstructions() + GetSampleExample();

    /// <summary>
    /// Loads source code context for the client library to aid sample generation.
    /// </summary>
    /// <param name="packagePath">Root path to the language-specific package.</param>
    /// <param name="totalBudget">Maximum aggregate characters for all source files.</param>
    /// <param name="perFileLimit">Maximum characters per individual file before truncation.</param>
    /// <param name="ct">Cancellation token applied to file reads.</param>
    /// <returns>Concatenated structured source context string.</returns>
    public abstract Task<string> GetClientLibrarySourceCodeAsync(string packagePath, int totalBudget, int perFileLimit, CancellationToken ct = default);

    /// <summary>
    /// Shared priority calculator for source inputs. Languages may override if needed, but most use the
    /// heuristic: filenames containing "client" get highest priority.
    /// </summary>
    /// <param name="f">File metadata.</param>
    /// <returns>Smaller numbers indicate higher priority.</returns>
    protected virtual int GetSourcePriority(FileMetadata f)
    {
        var name = Path.GetFileNameWithoutExtension(f.FilePath);
        return name.Contains("client", StringComparison.OrdinalIgnoreCase) ? 1 : 10;
    }
}
