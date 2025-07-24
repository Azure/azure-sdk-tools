using System.Text;
using System.Text.Json.Serialization;


namespace Azure.Sdk.Tools.Cli.Models.Responses
{
    public class TspToolResponse : Response
    {
        [JsonPropertyName("typespec_project_path")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string TypeSpecProjectPath { get; set; } = string.Empty;

        [JsonPropertyName("is_successful")]
        public bool IsSuccessful { get; set; }

        [JsonPropertyName("error_message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string ErrorMessage { get; set; } = string.Empty;

        public override string ToString()
        {
            StringBuilder output = new StringBuilder();

            output.AppendLine($"### Is Successful: {IsSuccessful}");
            if (IsSuccessful)
            {
                output.AppendLine($"### TypeSpec Project Path: {TypeSpecProjectPath}");
            }
            else
            {
                output.AppendLine($"### Error Message: {ErrorMessage}");
            }

            return output.ToString();
        }
    }
}
