using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models
{
    public class CommandResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("result")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Result { get; set; }

        [JsonPropertyName("duration")]
        public long Duration { get; set; }
    }
}
