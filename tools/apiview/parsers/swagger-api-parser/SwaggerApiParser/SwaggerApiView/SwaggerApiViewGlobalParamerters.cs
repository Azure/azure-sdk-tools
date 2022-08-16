using System.Collections.Generic;
using APIView;

namespace SwaggerApiParser;

public class SwaggerApiViewGlobalParameters : Dictionary<string, SwaggerApiViewParameter>, ITokenSerializable, INavigable
{
    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        ret.Add(TokenSerializer.FoldableContentStart());
        foreach (var parameter in this)
        {
            ret.Add(TokenSerializer.NavigableToken(parameter.Key, CodeFileTokenKind.MemberName, context.IteratorPath.CurrentNextPath(parameter.Key)));
            ret.Add(TokenSerializer.Colon());
            ret.Add(TokenSerializer.NewLine());
            ret.AddRange(parameter.Value.TokenSerialize(context));
        }

        ret.Add(TokenSerializer.FoldableContentEnd());
        return ret.ToArray();
    }

    public NavigationItem BuildNavigationItem(IteratorPath iteratorPath = null)
    {
        iteratorPath ??= new IteratorPath();
        NavigationItem ret = new NavigationItem() {Text = "Parameters"};
        iteratorPath.Add("Parameters");
        List<NavigationItem> children = new List<NavigationItem>();
        foreach (var kv in this)
        {
            children.Add(new NavigationItem() {Text = kv.Key, NavigationId = iteratorPath.CurrentNextPath(kv.Key)});
        }

        ret.ChildItems = children.ToArray();
        iteratorPath.Pop();
        return ret;
    }
}
