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
        ret.Add(TokenSerializer.Intent(context.intent));
        ret.Add(new CodeFileToken(this.statusCode, CodeFileTokenKind.Keyword));
        ret.Add(TokenSerializer.Colon());
        ret.Add(TokenSerializer.NewLine());

        ret.AddRange(TokenSerializer.TokenSerialize(this, context.intent + 1, new string[] {"description", "schema"}));

        return ret.ToArray();
    }
}
