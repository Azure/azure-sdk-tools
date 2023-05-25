using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwaggerApiParser.Specs
{
    public class ExternalDocs : ITokenSerializable
    {
        public string description { get; set; }
        public string url { get; set; }
        [JsonExtensionData]
        public IDictionary<string, dynamic> patternedObjects { get; set; }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            if (description != null)
                ret.AddRange(TokenSerializer.KeyValueTokens("description", description));

            if (url != null)
                ret.AddRange(TokenSerializer.KeyValueTokens("url", url));

            Utils.SerializePatternedObjects(patternedObjects, ret, context);

            return ret.ToArray();
        }
    }
}
