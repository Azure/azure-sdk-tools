// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.SampleGeneration.Languages;

public class GoSourceInputProvider : ILanguageSourceInputProvider
{
    public IReadOnlyList<FileHelper.SourceInput> Create(string packagePath)
    {
        return new List<FileHelper.SourceInput>
        {
            new FileHelper.SourceInput(
                Path: packagePath,
                IncludeExtensions: [".go"],
                ExcludeGlobPatterns: [
                    "**/vendor/**",
                    "**/bin/**"
                ])
        };
    }
}
