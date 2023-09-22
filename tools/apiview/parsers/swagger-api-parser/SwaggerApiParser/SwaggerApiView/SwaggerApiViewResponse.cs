using System.Collections.Generic;
using SwaggerApiParser.Specs;

namespace SwaggerApiParser.SwaggerApiView
{
    public class SwaggerApiViewResponse : ITokenSerializable
    {
        public string statusCode { get; set; }
        public string description { get; set; }
        public Schema schema { get; set; }

        public Dictionary<string, Header> headers { get; set; }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            context.IteratorPath.Add(this.statusCode);
            ret.Add(TokenSerializer.NavigableToken(this.statusCode, CodeFileTokenKind.Keyword, context.IteratorPath.CurrentPath()));
            ret.Add(TokenSerializer.Colon());
            ret.Add(TokenSerializer.NewLine());

            if (headers.Count > 0)
            {
                ret.Add(TokenSerializer.NavigableToken("headers", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentPath()));
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                foreach (var header in headers)
                {
                    ret.Add(new CodeFileToken(header.Key, CodeFileTokenKind.Keyword));
                    ret.Add(TokenSerializer.Colon());
                    ret.Add(TokenSerializer.NewLine());
                    ret.AddRange(header.Value.TokenSerialize(context));
                }
                ret.Add(TokenSerializer.NewLine());

            }


            ret.AddRange(TokenSerializer.KeyValueTokens("description", this.description, true, context.IteratorPath.CurrentNextPath("description")));
            if (this.schema != null)
            {
                ret.AddRange(this.schema.TokenSerialize(new SerializeContext(context.indent + 2, context.IteratorPath, context.definitionsNames)));
            }

            return ret.ToArray();
        }
    }
}
