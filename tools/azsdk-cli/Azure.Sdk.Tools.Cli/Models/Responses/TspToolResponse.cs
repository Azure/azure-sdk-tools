using System.Text.Json.Serialization;


namespace Azure.Sdk.Tools.Cli.Models.Responses
{
    public class TspToolResponse : CommandResponse
    {
        [JsonPropertyName("typespec_project_path")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string TypeSpecProjectPath { get; set; } = string.Empty;

        [JsonPropertyName("is_successful")]
        public bool IsSuccessful { get; set; }

        public override string ToString()
        {
            if (!IsSuccessful)
            {
                return ToString(string.Empty);
            }
            else
            {
                return string.Join(
                    Environment.NewLine,
                    [
                        $"### TypeSpec Project Path: {TypeSpecProjectPath}",
                        string.Empty,
                        ..this.NextSteps ?? Enumerable.Empty<string>()
                    ]
                );
            }
        }
    }
}
