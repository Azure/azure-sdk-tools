// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models
{
    public class ApiviewData
    {
        [JsonPropertyName("Language")]
        public string Language { get; set; } = string.Empty;
        [JsonPropertyName("Package Name")]
        public string PackageName { get; set; } = string.Empty;
        [JsonPropertyName("SDK API View Link")]
        public string ApiviewLink {  get; set; } = string.Empty;
    }
}
