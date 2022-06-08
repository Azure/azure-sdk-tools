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

    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();

        // Token Serialize "General" section.
        ret.Add(TokenSerializer.Intent(context.intent));
        context.IteratorPath.Add("General");
        ret.Add(TokenSerializer.NavigableToken("General", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentPath()));
        ret.Add(TokenSerializer.Colon());
        ret.Add(TokenSerializer.NewLine());
        var generalTokens = this.SwaggerApiViewGeneral.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath));
        ret.AddRange(generalTokens);
        context.IteratorPath.Pop();


        // Token serialize "Paths" section.
        ret.Add(TokenSerializer.Intent(context.intent));
        context.IteratorPath.Add("Paths");
        ret.Add(TokenSerializer.NavigableToken("Path", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentPath()));
        ret.Add(TokenSerializer.Colon());
        ret.Add(TokenSerializer.NewLine());
        var pathTokens = this.Paths.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath));
        ret.AddRange(pathTokens);
        context.IteratorPath.Pop();

        return ret.ToArray();
    }

    public NavigationItem[] BuildNavigationItems(IteratorPath iteratorPath = null)
    {
        iteratorPath ??= new IteratorPath();
        List<NavigationItem> ret = new List<NavigationItem>();

        ret.Add(this.SwaggerApiViewGeneral.BuildNavigationItem(iteratorPath));
        ret.Add(this.Paths.BuildNavigationItem(iteratorPath));
        return ret.ToArray();
    }

    public CodeFile GenerateCodeFile()
    {
        SerializeContext context = new SerializeContext();
        context.IteratorPath.Add(this.fileName);
        CodeFile ret = new CodeFile()
        {
            Tokens = this.TokenSerialize(context),
            Language = "Swagger",
            VersionString = "0",
            Name = this.fileName,
            PackageName = this.packageName,
            Navigation = new NavigationItem[] {this.BuildNavigationItem()}
        };
        return ret;
    }

    public NavigationItem BuildNavigationItem(IteratorPath iteratorPath = null)
    {
        iteratorPath ??= new IteratorPath();
        iteratorPath.Add(this.fileName);
        NavigationItem ret = new NavigationItem {Text = this.fileName, ChildItems = this.BuildNavigationItems(iteratorPath)};
        return ret;
    }
}
