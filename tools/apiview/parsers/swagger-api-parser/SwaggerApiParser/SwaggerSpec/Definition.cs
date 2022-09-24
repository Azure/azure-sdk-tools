using System.Collections.Generic;
using APIView;

namespace SwaggerApiParser;

public class Definition : BaseSchema
{
    public CodeFileToken[] TokenSerializeDefinition(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        ret.Add(new CodeFileToken(this.description, CodeFileTokenKind.Literal));
        ret.Add(TokenSerializer.NewLine());
        return ret.ToArray();
    }
    
    
}
