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

        protected override string Format()
        {
            if (!IsSuccessful)
            {
                return string.Empty;
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
