// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Immutable;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services
{
    public class LanguageService
    {
        private readonly ILanguageSpecificResolver<IPackageInfoHelper> _packageInfoResolver;
        private readonly ILogger<LanguageService> _logger;

        public LanguageService(ILanguageSpecificResolver<IPackageInfoHelper> packageInfoResolver, ILogger<LanguageService> logger)
        {
            _packageInfoResolver = packageInfoResolver;
            _logger = logger;
        }

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

        public async Task<PackageInfo?> GetPackageInfo(string packagePath, CancellationToken ct)
        {
            PackageInfo? packageInfo = null;
            try
            {
                var packageInfoHelper = await _packageInfoResolver.Resolve(packagePath, ct);
                if (packageInfoHelper != null)
                {
                    packageInfo = await packageInfoHelper.ResolvePackageInfo(packagePath, ct);
                }
                else
                {
                    _logger.LogError("No package info helper found for package path: {packagePath}", packagePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while parsing package path: {packagePath}", packagePath);
            }
            return packageInfo;
        }
    }
}
