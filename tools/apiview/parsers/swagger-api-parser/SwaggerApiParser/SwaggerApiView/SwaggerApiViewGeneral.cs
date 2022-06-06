using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
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
        return TokenSerializer.TokenSerialize(this, context.intent);
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
