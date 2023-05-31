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
            return this.@ref != null;
        }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            return TokenSerializer.TokenSerialize(this, context);
        }
    }
}
