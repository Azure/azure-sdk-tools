using System.Collections.Generic;

namespace SwaggerApiParser.Specs
{
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
}
