// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Attributes;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package
{
    public abstract class PackageResponseBase : CommandResponse
    {
        [Telemetry]
        [JsonPropertyName("language")]
        public SdkLanguage Language { get; set; }
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
    }
}
