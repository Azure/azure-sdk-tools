using System.Text.Json.Serialization;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser.Specs
{
    public class Reference : ITokenSerializable
    {
        [JsonPropertyName("$ref")]
        public string @ref { get; set; }

        public bool IsRefObject()
        {
            return !string.IsNullOrEmpty(this.@ref);
        }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            return TokenSerializer.TokenSerialize(this, context);
        }
    }
}
