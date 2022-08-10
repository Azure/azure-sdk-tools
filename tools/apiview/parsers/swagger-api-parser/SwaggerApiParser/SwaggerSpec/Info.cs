using System.Collections.Generic;
using APIView;

namespace SwaggerApiParser;

public class Info : ITokenSerializable
{
    public string title { get; set; }
    public string version { get; set; }
    public string description { get; set; }
    public string termsOfService { get; set; }

    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        ret.Add(TokenSerializer.FoldableContentStart());
        ret.AddRange(TokenSerializer.KeyValueTokens("title", title));
        ret.AddRange(TokenSerializer.KeyValueTokens("version", version));
        ret.AddRange(TokenSerializer.KeyValueTokens("description", description));
        ret.AddRange(TokenSerializer.KeyValueTokens("termsOfService", termsOfService));
        ret.Add(TokenSerializer.FoldableContentEnd());
        return ret.ToArray();
    }
}
