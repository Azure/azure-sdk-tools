using System.Collections.Generic;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser.Specs
{
    public class Definition : Schema
    {
        public CodeFileToken[] TokenSerializeDefinition(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            ret.Add(new CodeFileToken(this.description, CodeFileTokenKind.Literal));
            ret.Add(TokenSerializer.NewLine());
            return ret.ToArray();
        }
    }
}
