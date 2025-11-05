using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec
{
    /// <summary>
    /// Represents the response returned after validating a TypeSpec project, including validation results and messages.
    /// </summary>
    public class TypeSpecValidationResponse : TypeSpecBaseResponse
    {
        [JsonPropertyName("validation_results")]
        public List<string> validationResults = [];
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        protected override string Format()
        {
            var result = new StringBuilder();
            result.AppendLine($"TypeSpec project: {TypeSpecProject}");
            result.AppendLine($"SDK type: {PackageType}");
            result.AppendLine($"Message: {Message}");
            foreach ( var validationResult in validationResults )
            {
                result.AppendLine($"- {validationResult}");
            }
            return result.ToString();
        }
    }
}
