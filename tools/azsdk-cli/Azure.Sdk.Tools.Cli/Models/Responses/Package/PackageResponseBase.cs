// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Attributes;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package
{
    public abstract class PackageResponseBase : CommandResponse
    {
        private SdkLanguage _language = SdkLanguage.Unknown;

        [Telemetry]
        [JsonPropertyName("language")]
        public SdkLanguage Language
        {
            get
            {
                if (_language != SdkLanguage.Unknown)
                {
                    return _language;
                }
               return GetLanguageFromRepo(SdkRepoName);
            }
            set
            {
                _language = value;
            }
        }
        [Telemetry]
        [JsonPropertyName("package_name")]
        public string? PackageName { get; set; }
        [JsonPropertyName("package_display_name")]
        public string? DisplayName { get; set; }
        [JsonPropertyName("version")]
        public string? Version { get; set; }
        [Telemetry]
        [JsonPropertyName("package_type")]
        public SdkType PackageType { get; set; }
        [Telemetry]
        [JsonPropertyName("typespec_project")]
        public string? TypeSpecProject { get; set; }
        [JsonPropertyName("sdk_repo")]
        public string? SdkRepoName { get; set; }


        public void SetLanguage(string language)
        {
            if (Enum.TryParse<SdkLanguage>(language, true, out var lang))
            {
                Language = lang;
            }
        }
        public void SetPackageType(string packageType)
        {
            if (Enum.TryParse<SdkType>(packageType, true, out var type))
            {
                PackageType = type;
            }
        }

        public static SdkLanguage GetLanguageFromRepo(string? repoName)
        {
            if (string.IsNullOrEmpty(repoName))
            {
                return SdkLanguage.Unknown;
            }
            repoName = repoName.ToLower();
            return repoName switch
            {
                "azure-sdk-for-net" => SdkLanguage.DotNet,
                "azure-sdk-for-java" => SdkLanguage.Java,
                "azure-sdk-for-python" => SdkLanguage.Python,
                "azure-sdk-for-js" => SdkLanguage.JavaScript,
                "azure-sdk-for-go" => SdkLanguage.Go,
                _ => SdkLanguage.Unknown
            };
        }
    }
}
