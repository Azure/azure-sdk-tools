using System.Text.Json.Serialization;


namespace Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec
{
    public class TspToolResponse : TypeSpecBaseResponse
    {
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
                        $"### TypeSpec Project Path: {TypeSpecProject}",
                        string.Empty,
                        ..this.NextSteps ?? Enumerable.Empty<string>()
                    ]
                );
            }
        }
    }
}
