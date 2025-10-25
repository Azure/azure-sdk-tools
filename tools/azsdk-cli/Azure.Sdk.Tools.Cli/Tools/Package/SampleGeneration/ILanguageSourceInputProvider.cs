// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.SampleGeneration;

/// <summary>
/// Provides language-specific source input discovery for sample generation context gathering.
/// </summary>
public interface ILanguageSourceInputProvider
{
    /// <summary>
    /// Returns the list of source inputs (files/directories with filters) for the given package root.
    /// </summary>
    IReadOnlyList<SourceInput> Create(string packagePath);
}
