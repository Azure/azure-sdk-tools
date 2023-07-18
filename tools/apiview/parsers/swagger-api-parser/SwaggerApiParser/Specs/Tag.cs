using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser.Specs
{
    public class Tag : ITokenSerializable
    {
        public string name { get; set; }
        public string description { get; set; }
        public ExternalDocs externalDocs { get; set; }
        [JsonExtensionData]
        public IDictionary<string, JsonElement> patternedObjects { get; set; }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            ret.AddRange(TokenSerializer.KeyValueTokens("name", name, true, context.IteratorPath.CurrentNextPath("name")));

            if (description != null)
                ret.AddRange(TokenSerializer.KeyValueTokens("description", description, true, context.IteratorPath.CurrentNextPath("name")));

            if (externalDocs != null)
            {
                ret.Add(new CodeFileToken("externalDocs", CodeFileTokenKind.FoldableSectionHeading));
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                ret.Add(TokenSerializer.FoldableContentStart());
                ret.AddRange(externalDocs.TokenSerialize(context));
                ret.Add(TokenSerializer.FoldableContentEnd());
            }

            Utils.SerializePatternedObjects(patternedObjects, ret);
            return ret.ToArray();
        }
    }
}
