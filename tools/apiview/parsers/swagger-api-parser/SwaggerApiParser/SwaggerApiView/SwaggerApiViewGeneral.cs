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

    public Security security { get; set; }

    public SecurityDefinitions securityDefinitions { get; set; }
    
    public XMsParameterizedHost xMsParameterizedHost { get; set; }
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

        ret.AddRange(TokenSerializer.KeyValueTokens("swagger", swagger, true, context.IteratorPath.CurrentNextPath("swagger")));
        ret.Add(new CodeFileToken("info", CodeFileTokenKind.FoldableSectionHeading));
        ret.Add(TokenSerializer.Colon());
        ret.Add(TokenSerializer.NewLine());
        ret.AddRange(info.TokenSerialize(context));

        if (schemes != null)
        {
            var schemesStr = String.Join(",", schemes);
            ret.AddRange(TokenSerializer.KeyValueTokens("schemes", schemesStr, true,context.IteratorPath.CurrentNextPath("schemes")));
        }

        if (consumes != null)
        {
            var consumesStr = String.Join(",", consumes);
            ret.AddRange(TokenSerializer.KeyValueTokens("consumes", consumesStr, true, context.IteratorPath.CurrentNextPath("consumes")));
        }

        if (produces != null)
        {
            var producesStr = String.Join(",", produces);
            ret.AddRange(TokenSerializer.KeyValueTokens("produces", producesStr, true, context.IteratorPath.CurrentNextPath("produces")));
        }

        if (security != null)
        {
            string securityStr = "";
            foreach (var it in security)
            {
                foreach (var kv in it)
                {
                    securityStr += kv.Key + ": [" + string.Join(",", kv.Value) + "]";
                }
            }

            ret.AddRange(TokenSerializer.KeyValueTokens("security", securityStr, true, context.IteratorPath.CurrentNextPath("secuirty")));
        }

        if (securityDefinitions != null)
        {
            ret.Add(new CodeFileToken("securityDefinitions", CodeFileTokenKind.FoldableSectionHeading));
            ret.Add(TokenSerializer.Colon());
            ret.Add(TokenSerializer.NewLine());
            ret.Add(TokenSerializer.FoldableContentStart());
            foreach (var kv in securityDefinitions)
            {
                ret.Add(new CodeFileToken(kv.Key, CodeFileTokenKind.FoldableSectionHeading));
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                ret.AddRange(TokenSerializer.TokenSerializeAsJson(kv.Value, true));
            }
            ret.Add(TokenSerializer.FoldableContentEnd());
        }

        if (xMsParameterizedHost!=null)
        {
            ret.Add(new CodeFileToken("x-ms-parameterized-host", CodeFileTokenKind.FoldableSectionHeading));
            ret.Add(TokenSerializer.Colon());
            ret.Add(TokenSerializer.NewLine());
            ret.Add(TokenSerializer.FoldableContentStart());
            ret.AddRange(xMsParameterizedHost.TokenSerialize(context));
            ret.Add(TokenSerializer.FoldableContentEnd());
        }


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
