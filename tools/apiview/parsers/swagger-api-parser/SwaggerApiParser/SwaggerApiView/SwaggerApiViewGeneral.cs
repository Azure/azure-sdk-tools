using System;
using System.Collections.Generic;
using System.Text.Json;
using SwaggerApiParser.Specs;

namespace SwaggerApiParser.SwaggerApiView 
{
    public class SwaggerApiViewGeneral : ITokenSerializable, INavigable
    {
        public string swaggerLink { set; get; }
        public string swagger { set; get; }
        public Info info { set; get; }
        public string host { get; set; }
        public string basePath { get; set; }
        public List<string> schemes { get; set; }
        public List<string> consumes { get; set; }
        public List<string> produces { get; set; }
        public SecurityDefinitions securityDefinitions { get; set; }
        public Security security { get; set; }
        public List<Tag> tags { get; set; }
        public ExternalDocs externalDocs { get; set; }
        public Dictionary<string, JsonElement> patternedObjects { get; set; }
        public SchemaCache schemaCache { get; set; }
        public string swaggerFilePath { get; set; }

        public SwaggerApiViewGeneral()
        {
            this.info = new Info();
        }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();

            if (!String.IsNullOrEmpty(swaggerLink))
            {
                ret.AddRange(TokenSerializer.KeyValueTokens("swaggerLink", swaggerLink, true, context.IteratorPath.CurrentNextPath("swaggerLink")));
            }

            ret.AddRange(TokenSerializer.KeyValueTokens("swagger", swagger, true, context.IteratorPath.CurrentNextPath("swagger")));
            ret.Add(new CodeFileToken("info", CodeFileTokenKind.FoldableSectionHeading));
            ret.Add(TokenSerializer.Colon());
            ret.Add(TokenSerializer.NewLine());
            ret.Add(TokenSerializer.FoldableContentStart());
            ret.AddRange(info.TokenSerialize(context));
            ret.Add(TokenSerializer.FoldableContentEnd());

            if (!String.IsNullOrEmpty(host))
                ret.AddRange(TokenSerializer.KeyValueTokens("host", host, true, context.IteratorPath.CurrentNextPath("host")));

            if (!String.IsNullOrEmpty(basePath))
                ret.AddRange(TokenSerializer.KeyValueTokens("basePath", basePath, true, context.IteratorPath.CurrentNextPath("basePath")));

            if (schemes != null && schemes.Count > 0)
            {
                var schemesStr = String.Join(", ", schemes);
                ret.AddRange(TokenSerializer.KeyValueTokens("schemes", schemesStr, true, context.IteratorPath.CurrentNextPath("schemes")));
            }

            if (consumes != null && consumes.Count > 0)
            {
                var consumesStr = String.Join(", ", consumes);
                ret.AddRange(TokenSerializer.KeyValueTokens("consumes", consumesStr, true, context.IteratorPath.CurrentNextPath("consumes")));
            }

            if (produces != null && produces.Count > 0)
            {
                var producesStr = String.Join(", ", produces);
                ret.AddRange(TokenSerializer.KeyValueTokens("produces", producesStr, true, context.IteratorPath.CurrentNextPath("produces")));
            }

            if (securityDefinitions != null && securityDefinitions.Count > 0)
            {
                ret.Add(new CodeFileToken("securityDefinitions", CodeFileTokenKind.FoldableSectionHeading));
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                ret.Add(TokenSerializer.FoldableContentStart());
                foreach (var kv in securityDefinitions)
                {
                    ret.Add(new CodeFileToken(kv.Key, CodeFileTokenKind.FoldableSectionHeading));
                    ret.Add(TokenSerializer.Colon());
                    ret.Add(TokenSerializer.NewLine());
                    ret.AddRange(TokenSerializer.TokenSerializeAsJson(kv.Value, true));
                }
                ret.Add(TokenSerializer.FoldableContentEnd());
            }

            if (security != null && security.Count > 0)
            {
                string securityStr = "";
                foreach (var it in security)
                {
                    foreach (var kv in it)
                    {
                        securityStr += kv.Key + ": [" + string.Join(", ", kv.Value) + "]";
                    }
                }

                ret.AddRange(TokenSerializer.KeyValueTokens("security", securityStr, true, context.IteratorPath.CurrentNextPath("secuirty")));
            }

            if (tags != null && tags.Count > 0)
            {
                ret.Add(new CodeFileToken("tags", CodeFileTokenKind.FoldableSectionHeading));
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                ret.Add(TokenSerializer.FoldableContentStart());
                foreach (var tag in tags)
                {
                    if (tag != null)
                    {
                        ret.AddRange(tag.TokenSerialize(context));
                    }
                }
                ret.Add(TokenSerializer.FoldableContentEnd());
            }

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

        public NavigationItem BuildNavigationItem(IteratorPath iteratorPath = null)
        {
            iteratorPath ??= new IteratorPath();
            iteratorPath.Add("General");
            var ret = new NavigationItem() { Text = "General", NavigationId = iteratorPath.CurrentPath() };
            iteratorPath.Pop();
            return ret;
        }
    }
}
