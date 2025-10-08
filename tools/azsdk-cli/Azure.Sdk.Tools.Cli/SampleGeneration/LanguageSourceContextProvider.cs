// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.SampleGeneration.Languages;

namespace Azure.Sdk.Tools.Cli.SampleGeneration;

/// <summary>
/// Facade that delegates to language-specific providers for source input discovery.
/// </summary>
public static class LanguageSourceContextProvider
{
    private static readonly Dictionary<string, ILanguageSourceInputProvider> s_providers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dotnet"] = new DotNetSourceInputProvider(),
        ["java"] = new JavaSourceInputProvider(),
        ["typescript"] = new TypeScriptSourceInputProvider(),
        ["python"] = new PythonSourceInputProvider(),
        ["go"] = new GoSourceInputProvider(),
    };

    public static List<FileHelper.SourceInput> CreateSourceInputs(string packagePath, string language)
    {
        if (!s_providers.TryGetValue(language, out var provider))
        {
            throw new ArgumentException($"Unsupported language for source context loading: '{language}'. Supported languages are: {string.Join(", ", s_providers.Keys)}", nameof(language));
        }
        return provider.Create(packagePath).ToList();
    }
}
