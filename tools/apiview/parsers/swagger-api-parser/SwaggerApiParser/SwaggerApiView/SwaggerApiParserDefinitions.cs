using System;
using System.Collections.Generic;
using APIView;

namespace SwaggerApiParser;

public class SwaggerApiViewDefinitions : Dictionary<String, Definition>, INavigable, ITokenSerializable
{
    public NavigationItem BuildNavigationItem(IteratorPath iteratorPath = null)
    {
        iteratorPath ??= new IteratorPath();
        NavigationItem ret = new NavigationItem() {Text = "Definitions"};
        List<NavigationItem> children = new List<NavigationItem>();

        return ret;
    }

    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        ret.Add(TokenSerializer.FoldableContentStart());
        foreach (var kv in this)
        {
            ret.Add(new CodeFileToken(kv.Key, CodeFileTokenKind.Literal));
            ret.Add(TokenSerializer.Colon());
            ret.Add(TokenSerializer.NewLine());
            ret.AddRange(kv.Value.TokenSerialize(context));
        }
        ret.Add(TokenSerializer.FoldableContentEnd());
        return ret.ToArray();
    }
}
