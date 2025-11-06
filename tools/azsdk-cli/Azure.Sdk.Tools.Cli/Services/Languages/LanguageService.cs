// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Immutable;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Languages
{
    public class LanguageService
    {
        public static readonly ImmutableDictionary<string, SdkLanguage> RepoToLanguageMap = new Dictionary<string, SdkLanguage>()
        {
            { "azure-sdk-for-net", SdkLanguage.DotNet },
            { "azure-sdk-for-go", SdkLanguage.Go },
            { "azure-sdk-for-java", SdkLanguage.Java },
            { "azure-sdk-for-js", SdkLanguage.JavaScript },
            { "azure-sdk-for-python", SdkLanguage.Python },
            { "azure-sdk-for-rust", SdkLanguage.Rust}
        }.ToImmutableDictionary();

        public static SdkLanguage GetLanguageForRepo(string repoName)
        {
            if (string.IsNullOrEmpty(repoName))
            {
                return SdkLanguage.Unknown;
            }
            if (RepoToLanguageMap.TryGetValue(repoName.ToLower(), out SdkLanguage language))
            {
                return language;
            }
            return SdkLanguage.Unknown;
        }
    }
}
