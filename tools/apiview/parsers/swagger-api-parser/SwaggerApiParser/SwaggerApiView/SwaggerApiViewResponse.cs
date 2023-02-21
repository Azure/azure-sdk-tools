using System.Collections.Generic;

namespace SwaggerApiParser;

public class SwaggerApiViewResponse : ITokenSerializable
{
    public string statusCode { get; set; }
    public string description { get; set; }
    public BaseSchema schema { get; set; }

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
                ret.AddRange(TokenSerializer.KeyValueTokens(header.Key, ""));
                ret.AddRange(header.Value.TokenSerialize(context));
            }
            ret.Add(TokenSerializer.NewLine());
            
        }


        ret.AddRange(TokenSerializer.KeyValueTokens("description", this.description, true, context.IteratorPath.CurrentNextPath("description")));
        if (this.schema != null)
        {
            ret.AddRange(this.schema.TokenSerialize(new SerializeContext(context.intent + 2, context.IteratorPath)));
        }

        return ret.ToArray();
    }
}
