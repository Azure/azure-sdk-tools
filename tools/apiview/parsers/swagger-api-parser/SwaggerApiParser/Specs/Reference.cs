using System.Text.Json.Serialization;

namespace SwaggerApiParser.Specs
{
    public class Reference : IBaseReference
    {
        [JsonPropertyName("$ref")]
        public string @ref { get; set; }
    }
}
