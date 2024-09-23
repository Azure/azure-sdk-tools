using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser.Specs
{
    public class License : ITokenSerializable
    {
        public string name { get; set; }
        public string url { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement> patternedObjects { get; set; }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            if (name != null)
                ret.AddRange(TokenSerializer.KeyValueTokens("name", name, true, context.IteratorPath.CurrentNextPath("name")));

            if (url != null)
                ret.AddRange(TokenSerializer.KeyValueTokens("url", url, true, context.IteratorPath.CurrentNextPath("url")));

            Utils.SerializePatternedObjects(patternedObjects, ret);

            return ret.ToArray();
        }
    }
}
