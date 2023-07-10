using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser.Specs
{
    public class Header : Items
    {
        public string description { get; set; }

        public new CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            ret.AddRange(TokenSerializer.KeyValueTokens("type", type, true, context.IteratorPath.CurrentNextPath("type")));

            if (description != null)
                ret.AddRange(TokenSerializer.KeyValueTokens("description", description, true, context.IteratorPath.CurrentNextPath("description")));
            
            return ret.ToArray();
        }
    }
}
