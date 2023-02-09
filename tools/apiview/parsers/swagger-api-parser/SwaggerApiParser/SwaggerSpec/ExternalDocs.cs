using System.Collections.Generic;

namespace SwaggerApiParser;

public class ExternalDocs : ITokenSerializable
{
    public string description { get; set; }
    public string url { get; set; }

    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        if (description != null)
        {
            ret.AddRange(TokenSerializer.KeyValueTokens("description", description));
        }

        if (url != null)
        {
            ret.AddRange(TokenSerializer.KeyValueTokens("url", url));
        }

        return ret.ToArray();
    }
}