using System.Collections.Generic;
using APIView;

namespace SwaggerApiParser;

public class Info : ITokenSerializable
{
    public string title { get; set; }
    public string version { get; set; }
    public string description { get; set; }
    public string termsOfService { get; set; }

    public ExternalDocs externalDocs { get; set; }

    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        ret.Add(TokenSerializer.FoldableContentStart());
        ret.AddRange(TokenSerializer.KeyValueTokens("title", title, true, context.IteratorPath.CurrentNextPath("title")));
        ret.AddRange(TokenSerializer.KeyValueTokens("version", version, true, context.IteratorPath.CurrentNextPath("version")));
        ret.AddRange(TokenSerializer.KeyValueTokens("description", description, true, context.IteratorPath.CurrentNextPath("description")));

        if (termsOfService != null)
        {
            ret.AddRange(TokenSerializer.KeyValueTokens("termsOfService", termsOfService, true, context.IteratorPath.CurrentNextPath("termsOfService")));
        }

        if (externalDocs != null)
        {
            ret.Add(new CodeFileToken("externalDocs", CodeFileTokenKind.FoldableSectionHeading));
            ret.Add(TokenSerializer.Colon());
            ret.Add(TokenSerializer.NewLine());
            ret.Add(TokenSerializer.FoldableContentStart());
            ret.AddRange(externalDocs.TokenSerialize(context));
            ret.Add(TokenSerializer.FoldableContentEnd());
        }

        ret.Add(TokenSerializer.FoldableContentEnd());
        return ret.ToArray();
    }
}
