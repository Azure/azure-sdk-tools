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
                _language = SdkLanguageHelpers.GetLanguageForRepo(SdkRepoName);
                return _language;
            }
            set
            {
                _language = value;
            }
        }

        /// <summary>
        /// The package name, within the package ecosystem (ie: azure-core, @azure/core, etc..). 
        /// Go uses the sub-path as the package name (sdk/azcore, sdk/resourcemanager/msi/armmsi).
        /// </summary>
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

        public PackageResponseBase() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageResponseBase"/> class with the specified
        /// SDK language and package name.
        /// </summary>
        /// <param name="packageName">The package identifier or name.</param>
        /// <param name="language">The SDK language for the package.</param>
        public PackageResponseBase(string packageName, SdkLanguage language)
        {
            ArgumentException.ThrowIfNullOrEmpty(packageName, nameof(packageName));

            if (language == SdkLanguage.Unknown)
            {
                throw new ArgumentException($"language cannot be {SdkLanguage.Unknown}", nameof(language));
            }

            PackageName = packageName;
            Language = language;
        }

        public void SetLanguage(string language)
        {
            Language = SdkLanguageHelpers.GetSdkLanguage(language);
        }
        public void SetPackageType(string packageType)
        {
            PackageType = packageType.ToLower() switch
            {
                "client" => SdkType.Dataplane,
                "mgmt" => SdkType.Management,
                "spring" => SdkType.Spring,
                _ => SdkType.Unknown,
            };
        }
    }
}
