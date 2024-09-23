using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser.Specs
{
    public class XML : ITokenSerializable
    {
        public string name { get; set; }
        [JsonPropertyName("namespace")]
        public string @namespace { get; set; }
        public string prefix { get; set; }
        public bool attribute { get; set; }
        public bool wrapped { get; set; }
        [JsonExtensionData]
        public IDictionary<string, JsonElement> patternedObjects { get; set; }
        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            return TokenSerializer.TokenSerialize(this, context);
        }
    }
}
