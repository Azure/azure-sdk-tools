// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package
{
    public class PackageBuildResponse: PackageResponseBase
    {
        private static readonly JsonSerializerOptions serializerOptions = new()
        {
            WriteIndented = true
        };

        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Message { get; set; }

        [JsonPropertyName("result")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Result { get; set; }

        [JsonPropertyName("duration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long Duration { get; set; }

        protected override string Format()
        {
            var output = new StringBuilder();
            if (!string.IsNullOrEmpty(Message))
            {
                output.AppendLine(Message);
            }
            if (Result != null)
            {
                if (Result is System.Collections.IEnumerable enumerable && Result is not string)
                {
                    var outputs = enumerable.Cast<object>().Select(item => item?.ToString());
                    foreach (var item in outputs)
                    {
                        output.AppendLine(item);
                    }
                }
                else
                {
                    output.AppendLine(JsonSerializer.Serialize(Result, serializerOptions));
                }
            }
            if (Duration > 0)
            {
                output.AppendLine($"Duration: {Duration}ms");
            }
            if (!string.IsNullOrEmpty(PackageName))
            {
                output.AppendLine($"Package PackageName: {PackageName}");
            }
            if (Language != SdkLanguage.Unknown)
            {
                output.AppendLine($"Language: {Language}");
            }
            if (!string.IsNullOrEmpty(Version))
            {
                output.AppendLine($"Version: {Version}");
            }
            if (PackageType != SdkType.Unknown)
            {
                output.AppendLine($"Package Type: {PackageType}");
            }

            return output.ToString();
        }

        public static implicit operator PackageBuildResponse(string s) => new() { Message = s };
    }
}
