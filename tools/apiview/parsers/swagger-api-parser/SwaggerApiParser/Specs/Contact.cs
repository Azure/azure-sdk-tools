using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser.Specs
{
    public class Contact : ITokenSerializable
    {
        public string name { get; set; }
        public string url { get; set; }
        public string email { get; set; }
        [JsonExtensionData]
        public IDictionary<string, JsonElement> patternedObjects { get; set; }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            if (name != null)
                ret.AddRange(TokenSerializer.KeyValueTokens("name", name, true, context.IteratorPath.CurrentNextPath("name")));

            if (url != null)
                ret.AddRange(TokenSerializer.KeyValueTokens("url", url, true, context.IteratorPath.CurrentNextPath("url")));

            if (email != null)
                ret.AddRange(TokenSerializer.KeyValueTokens("email", email, true, context.IteratorPath.CurrentNextPath("email")));

            Utils.SerializePatternedObjects(patternedObjects, ret);

            return ret.ToArray();
        }
    }
}
