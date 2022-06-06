using System;
using System.Collections.Generic;
using ApiView;
using APIView;

namespace SwaggerApiParser;

public class SwaggerApiViewRoot : ITokenSerializable
{
    public String ResourceProvider;
    public String PackageName;
    public Dictionary<String, SwaggerApiViewSpec> SwaggerApiViewSpecs;

    public CodeFile GenerateCodeFile()
    {
        CodeFile ret = new CodeFile()
        {
            Tokens = this.TokenSerialize(),
            Language = "Swagger",
            VersionString = "0",
            Name = this.ResourceProvider,
            PackageName = this.PackageName,
            Navigation = this.BuildNavigationItems()
        };

        return ret;
    }


    public CodeFileToken[] TokenSerialize(int intent = 0)
    {
        CodeFileToken[] ret = new CodeFileToken[] { };
        return ret;
    }

    public NavigationItem[] BuildNavigationItems()
    {
        List<NavigationItem> ret = new List<NavigationItem>();
        foreach (var swaggerApiViewSpec in this.SwaggerApiViewSpecs)
        {
            ret.Add(swaggerApiViewSpec.Value.BuildNavigationItem());
        }

        return ret.ToArray();
    }
}
