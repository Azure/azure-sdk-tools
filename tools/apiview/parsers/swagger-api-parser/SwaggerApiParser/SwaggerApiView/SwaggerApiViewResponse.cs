using System;
using System.Collections.Generic;
using APIView;

namespace SwaggerApiParser;

public class SwaggerApiViewResponse : ITokenSerializable
{
    public string statusCode { get; set; }
    public string description { get; set; }
    public BaseSchema schema { get; set; }

    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        context.IteratorPath.Add(this.statusCode);
        ret.Add(TokenSerializer.NavigableToken(this.statusCode, CodeFileTokenKind.Keyword, context.IteratorPath.CurrentPath()));
        ret.Add(TokenSerializer.Colon());
        ret.Add(TokenSerializer.NewLine());

        ret.AddRange(TokenSerializer.KeyValueTokens("description", this.description, true, context.IteratorPath.CurrentNextPath("description")));
        if (this.schema != null)
        {
            ret.AddRange(this.schema.TokenSerialize(new SerializeContext(context.intent + 2, context.IteratorPath)));
        }

        return ret.ToArray();
    }
}
