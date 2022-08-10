using System;
using System.Collections.Generic;
using System.Linq;
using APIView;

namespace SwaggerApiParser;

public class SwaggerApiViewGeneral : ITokenSerializable, INavigable
{
    public string swagger { set; get; }
    public Info info { set; get; }

    public string host { get; set; }
    public List<string> schemes { get; set; }
    public List<string> consumes { get; set; }
    public List<string> produces { get; set; }

    public SwaggerApiViewGeneral()
    {
        this.info = new Info();
    }

    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();

        ret.AddRange(TokenSerializer.KeyValueTokens("swagger", swagger));
        ret.Add(new CodeFileToken("info", CodeFileTokenKind.FoldableParentToken));
        ret.Add(TokenSerializer.Colon());
        ret.Add(TokenSerializer.NewLine());
        ret.AddRange(info.TokenSerialize(context));

        var schemesStr = String.Join(",", schemes);
        ret.AddRange(TokenSerializer.KeyValueTokens("schemes", schemesStr));

        var consumesStr = String.Join(",", consumes);
        ret.AddRange(TokenSerializer.KeyValueTokens("consumes", consumesStr));
        
        var producesStr = String.Join(",", produces);
        ret.AddRange(TokenSerializer.KeyValueTokens("produces", producesStr));
        return ret.ToArray();
    }

    public NavigationItem BuildNavigationItem(IteratorPath iteratorPath = null)
    {
        iteratorPath ??= new IteratorPath();
        iteratorPath.Add("General");
        var ret = new NavigationItem() {Text = "General", NavigationId = iteratorPath.CurrentPath()};
        iteratorPath.Pop();
        return ret;
    }
}
