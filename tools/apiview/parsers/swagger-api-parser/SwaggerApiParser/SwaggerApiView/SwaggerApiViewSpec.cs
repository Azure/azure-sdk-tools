using System.Collections.Generic;
using ApiView;
using APIView;

namespace SwaggerApiParser;

public class SwaggerApiViewSpec : INavigable, ITokenSerializable
{
    public SwaggerApiViewSpec(string fileName)
    {
        this.fileName = fileName;
    }

    public SwaggerApiViewGeneral SwaggerApiViewGeneral { get; set; }
    public SwaggerApiViewPaths Paths { get; set; }


    public string fileName;
    public string packageName;


    public SwaggerApiViewSpec()
    {
        this.Paths = new SwaggerApiViewPaths();
        this.SwaggerApiViewGeneral = new SwaggerApiViewGeneral();
    }

    public CodeFileToken[] TokenSerialize(int intent = 0)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();

        ret.Add(TokenSerializer.Intent(intent));
        ret.Add(new CodeFileToken("General", CodeFileTokenKind.Keyword));
        ret.Add(new CodeFileToken(":", CodeFileTokenKind.Punctuation));
        ret.Add(TokenSerializer.NewLine());
        var generalTokens = this.SwaggerApiViewGeneral.TokenSerialize(intent + 1);
        ret.AddRange(generalTokens);


        ret.Add(TokenSerializer.Intent(intent));
        ret.Add(new CodeFileToken("Path", CodeFileTokenKind.Keyword));
        ret.Add(new CodeFileToken(":", CodeFileTokenKind.Punctuation));
        ret.Add(TokenSerializer.NewLine());
        var pathTokens = this.Paths.TokenSerialize(intent + 1);
        ret.AddRange(pathTokens);

        return ret.ToArray();
    }

    public NavigationItem[] BuildNavigationItems()
    {
        List<NavigationItem> ret = new List<NavigationItem>();
        ret.Add(this.SwaggerApiViewGeneral.BuildNavigationItem());
        ret.Add(this.Paths.BuildNavigationItem());
        return ret.ToArray();
    }

    public CodeFile GenerateCodeFile()
    {
        CodeFile ret = new CodeFile()
        {
            Tokens = this.TokenSerialize(),
            Language = "Swagger",
            VersionString = "0",
            Name = this.fileName,
            PackageName = this.packageName,
            Navigation = new NavigationItem[] {this.BuildNavigationItem()}
        };
        return ret;
    }

    public NavigationItem BuildNavigationItem()
    {
        NavigationItem ret = new NavigationItem {Text = this.fileName, ChildItems = this.BuildNavigationItems()};
        return ret;
    }
}
