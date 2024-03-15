using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace AzureSdkQaBot.Model
{
    public class RagChunks
    {
        [JsonProperty("chunks")]
        public IList<RagChunk>? Chunks { get; set; }
    }

    public class RagChunk
    {
        [JsonProperty("id")]
        public required string Id { get; set; }

        [JsonProperty("text")]
        public string? Text { get; set; }

        [JsonProperty("documentTitle")]
        public string? DocumentTitle { get; set; }

        [JsonProperty("documentLink")]
        public required string DocumentLink { get; set; }

        [JsonProperty("headingTitle")]
        public string? HeadingTitle { get; set; }

        [JsonProperty("headingLink")]
        public string? headingLink { get; set; }

        [JsonProperty("ragText")]
        public string? RagText { get; set; }

        [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
        public RagTextType RagTextType { get; set; }
    }

    public enum RagTextType
    {
        [EnumMember(Value = "heading")]
        Heading,

        [EnumMember(Value = "larger")]
        Larger
    }
}
