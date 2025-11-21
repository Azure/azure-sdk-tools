// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services.Languages.Samples;

public class GoSourceInputProvider : ILanguageSourceInputProvider
{
    public IReadOnlyList<SourceInput> Create(string packagePath)
    {
        return
        [
            new(
                Path: packagePath,
                IncludeExtensions: [".go"])
        ];
    }
}
