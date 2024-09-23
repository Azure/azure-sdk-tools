using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser.Specs
{
    public class Info : ITokenSerializable
    {
        public string title { get; set; }
        public string description { get; set; }
        public string termsOfService { get; set; }
        public Contact contact { get; set; }
        public License license { get; set; }
        public string version { get; set; }
        [JsonExtensionData]
        public IDictionary<string, JsonElement> patternedObjects { get; set; }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            ret.AddRange(TokenSerializer.KeyValueTokens("title", title, true, context.IteratorPath.CurrentNextPath("title")));

            if (description != null)
                ret.AddRange(TokenSerializer.KeyValueTokens("description", description, true, context.IteratorPath.CurrentNextPath("description")));

            if (termsOfService != null)
                ret.AddRange(TokenSerializer.KeyValueTokens("termsOfService", termsOfService, true, context.IteratorPath.CurrentNextPath("termsOfService")));

            if (contact != null)
            {
                ret.Add(new CodeFileToken("contact", CodeFileTokenKind.FoldableSectionHeading));
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                ret.Add(TokenSerializer.FoldableContentStart());
                ret.AddRange(contact.TokenSerialize(context));
                ret.Add(TokenSerializer.FoldableContentEnd());
            }

            if (license != null)
            {
                ret.Add(new CodeFileToken("license", CodeFileTokenKind.FoldableSectionHeading));
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                ret.Add(TokenSerializer.FoldableContentStart());
                ret.AddRange(license.TokenSerialize(context));
                ret.Add(TokenSerializer.FoldableContentEnd());
            }

            ret.AddRange(TokenSerializer.KeyValueTokens("version", version, true, context.IteratorPath.CurrentNextPath("version")));

            Utils.SerializePatternedObjects(patternedObjects, ret);
            
            return ret.ToArray();
        }
    }
}
