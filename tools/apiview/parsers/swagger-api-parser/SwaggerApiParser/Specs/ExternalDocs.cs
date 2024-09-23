using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser.Specs
{
    public class ExternalDocs : ITokenSerializable
    {
        public string description { get; set; }
        public string url { get; set; }
        [JsonExtensionData]
        public IDictionary<string, JsonElement> patternedObjects { get; set; }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            if (description != null)
                ret.AddRange(TokenSerializer.KeyValueTokens("description", description));

            if (url != null)
                ret.AddRange(TokenSerializer.KeyValueTokens("url", url));

            Utils.SerializePatternedObjects(patternedObjects, ret);

            return ret.ToArray();
        }
    }
}
