using System.Collections.Generic;

namespace SwaggerApiParser.SwaggerApiView
{ 
    public class SwaggerApiViewGlobalParameters : SortedDictionary<string, SwaggerApiViewParameter>, ITokenSerializable, INavigable
    {
        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            ret.Add(TokenSerializer.FoldableContentStart());
            foreach (var parameter in this)
            {
                context.IteratorPath.Add(parameter.Key);
                ret.Add(TokenSerializer.NavigableToken(parameter.Key, CodeFileTokenKind.MemberName, context.IteratorPath.CurrentPath()));
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                ret.AddRange(parameter.Value.TokenSerialize(context));
                context.IteratorPath.Pop();
            }

            ret.Add(TokenSerializer.FoldableContentEnd());
            return ret.ToArray();
        }

        public NavigationItem BuildNavigationItem(IteratorPath iteratorPath = null)
        {
            iteratorPath ??= new IteratorPath();
            iteratorPath.Add("Parameters");
            NavigationItem ret = new NavigationItem() { Text = "Parameters", NavigationId = iteratorPath.CurrentPath() };
            List<NavigationItem> children = new List<NavigationItem>();
            foreach (var kv in this)
            {
                children.Add(new NavigationItem() { Text = kv.Key, NavigationId = iteratorPath.CurrentNextPath(kv.Key) });
            }

            ret.ChildItems = children.ToArray();
            iteratorPath.Pop();
            return ret;
        }
    }
}
