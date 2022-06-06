using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using APIView;

namespace SwaggerApiParser;

public class SwaggerApiViewGeneral: ITokenSerializable
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

    public CodeFileToken[] TokenSerialize(int intent=0)
    {

        return TokenSerializer.TokenSerialize(this, intent);
    }

    public NavigationItem BuildNavigationItem()
    {
        return new NavigationItem() {Text = "General", NavigationId = "General"};
    }
}
