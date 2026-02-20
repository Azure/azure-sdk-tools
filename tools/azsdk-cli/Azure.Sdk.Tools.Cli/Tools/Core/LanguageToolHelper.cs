// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.Tools.Core;

/// <summary>
/// Encapsulates language service resolution logic shared by
/// <see cref="LanguageMcpTool"/> and <see cref="LanguageMcpMultiCommandTool"/>.
/// Both classes delegate to an instance of this helper to avoid duplicating
/// the language detection / lookup methods.
/// </summary>
internal sealed class LanguageToolHelper(
    IEnumerable<LanguageService> languageServices,
    IGitHelper gitHelper)
{
    public IEnumerable<LanguageService> LanguageServices { get; } = languageServices;
    public IGitHelper GitHelper { get; } = gitHelper;

#pragma warning disable MCP003 // Tool methods must return Response types, built-in value types, or string
    public async Task<LanguageService> GetLanguageServiceAsync(string packagePath, CancellationToken ct = default)
    {
        var language = await SdkLanguageHelpers.GetLanguageForRepoPathAsync(GitHelper, packagePath, ct);
        if (language == SdkLanguage.Unknown)
        {
            return null;
        }
        return GetLanguageService(language);
    }

    public LanguageService GetLanguageService(SdkLanguage language)
    {
        return LanguageServices.FirstOrDefault(s => s.Language == language);
    }
#pragma warning restore MCP003 // Tool methods must return Response types, built-in value types, or string
}
