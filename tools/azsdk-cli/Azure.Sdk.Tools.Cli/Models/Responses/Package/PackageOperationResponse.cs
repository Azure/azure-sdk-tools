// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package
{
    public class PackageOperationResponse: PackageResponseBase
    {
        private static readonly JsonSerializerOptions serializerOptions = new()
        {
            WriteIndented = true
        };

        // Standard next steps for failure scenarios
        private static readonly string[] StandardFailureNextSteps = [
            "Check the running logs for details about the error",
            "Resolve the issue",
            "Re-run the tool",
            "Run verify setup tool if the issue is environment related"
        ];

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
            output.AppendLine($"Package: {PackageName}");
            output.AppendLine($"Language: {Language}");
            output.AppendLine($"Version: {Version}");
            output.AppendLine($"Package Type: {PackageType}");
            return output.ToString();
        }

        public static implicit operator PackageOperationResponse(string s) => new() { Message = s };

        /// <summary>
        /// Creates a failure response with the specified error message and package info.
        /// </summary>
        /// <param name="message">The error message to include in the response.</param>
        /// <param name="packageInfo">Optional package information to include in the response.</param>
        /// <param name="nextSteps">Optional next steps to include in the response. If null, standard failure next steps will be used.</param>
        /// <param name="sdkRepoName">Optional SDK repository name to include in the response.</param>
        /// <param name="typespecProjectPath">Optional TypeSpec project path to include in the response.</param>
        /// <returns>A PackageOperationResponse indicating failure.</returns>
        public static PackageOperationResponse CreateFailure(string message, PackageInfo? packageInfo = null, string[]? nextSteps = null, string? sdkRepoName = null, string? typespecProjectPath = null)
        {
            return new PackageOperationResponse
            {
                ResponseErrors = [message],
                PackageName = packageInfo?.PackageName ?? string.Empty,
                Language = packageInfo?.Language ?? SdkLanguage.Unknown,
                PackageType = packageInfo?.SdkType ?? SdkType.Unknown,
                Result = "failed",
                NextSteps = nextSteps?.ToList() ?? StandardFailureNextSteps.ToList(),
                SdkRepoName = sdkRepoName ?? string.Empty,
                TypeSpecProject = typespecProjectPath ?? string.Empty
            };
        }

        /// <summary>
        /// Creates a success response with the specified message.
        /// </summary>
        /// <param name="message">The success message to include in the response.</param>
        /// <param name="packageInfo">Optional package information to include in the response.</param>
        /// <param name="nextSteps">Optional next steps to include in the response.</param>
        /// <param name="result">Optional result status to include in the response.</param>
        /// <param name="sdkRepoName">Optional SDK repository name to include in the response.</param>
        /// <param name="typespecProjectPath">Optional TypeSpec project path to include in the response.</param>
        /// <returns>A PackageOperationResponse indicating success.</returns>
        public static PackageOperationResponse CreateSuccess(string message, PackageInfo? packageInfo = null, string[]? nextSteps = null, string? result = "succeeded", string? sdkRepoName = null, string? typespecProjectPath = null)
        {
            return new PackageOperationResponse
            {
                Result = result,
                Message = message,
                PackageName = packageInfo?.PackageName ?? string.Empty,
                Version = packageInfo?.PackageVersion ?? string.Empty,
                Language = packageInfo?.Language ?? SdkLanguage.Unknown,
                PackageType = packageInfo?.SdkType ?? SdkType.Unknown,
                NextSteps = nextSteps?.ToList() ?? [],
                SdkRepoName = sdkRepoName ?? string.Empty,
                TypeSpecProject = typespecProjectPath ?? string.Empty
            };
        }
    }
}
