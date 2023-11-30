using System.Collections.Generic;

namespace SwaggerApiParser.SwaggerApiView
{
    public class SwaggerApiViewSpec : INavigable, ITokenSerializable
    {
        public SwaggerApiViewSpec(string fileName)
        {
            this.fileName = fileName;
        }

        public SwaggerApiViewGeneral SwaggerApiViewGeneral { get; set; }
        public SwaggerApiViewPaths Paths { get; set; }
        public SwaggerApiViewDefinitions SwaggerApiViewDefinitions { get; set; }
        public SwaggerApiViewGlobalParameters SwaggerApiViewGlobalParameters { get; set; }


        public string fileName;
        public string packageName;
        public string APIVersion;


        public SwaggerApiViewSpec()
        {
            this.Paths = new SwaggerApiViewPaths();
            this.SwaggerApiViewGeneral = new SwaggerApiViewGeneral();
            this.SwaggerApiViewDefinitions = new SwaggerApiViewDefinitions();
            this.SwaggerApiViewGlobalParameters = new SwaggerApiViewGlobalParameters();
        }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();

            context.IteratorPath.Add("General");
            var generalTokens = this.SwaggerApiViewGeneral.TokenSerialize(new SerializeContext(context.indent + 1, context.IteratorPath));
            ret.AddRange(generalTokens);
            context.IteratorPath.Pop();


            // Token serialize "Paths" section.
            context.IteratorPath.Add("Paths");
            ret.Add(TokenSerializer.NavigableToken("Paths", CodeFileTokenKind.FoldableSectionHeading, context.IteratorPath.CurrentPath()));
            ret.Add(TokenSerializer.Colon());
            ret.Add(TokenSerializer.NewLine());
            var pathTokens = this.Paths.TokenSerialize(new SerializeContext(context.indent + 1, context.IteratorPath, context.definitionsNames));
            ret.AddRange(pathTokens);
            context.IteratorPath.Pop();


            if (this.SwaggerApiViewDefinitions != null && this.SwaggerApiViewDefinitions.Count > 0)
            {
                context.IteratorPath.Add("Definitions");
                ret.Add(TokenSerializer.NavigableToken("Definitions", CodeFileTokenKind.FoldableSectionHeading, context.IteratorPath.CurrentPath()));
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                var definitionTokens = this.SwaggerApiViewDefinitions.TokenSerialize(new SerializeContext(context.indent + 1, context.IteratorPath, context.definitionsNames));
                ret.AddRange(definitionTokens);
                context.IteratorPath.Pop();
            }

            if (this.SwaggerApiViewGlobalParameters != null && this.SwaggerApiViewGlobalParameters.Count > 0)
            {
                context.IteratorPath.Add("Parameters");
                ret.Add(TokenSerializer.NavigableToken("Parameters", CodeFileTokenKind.FoldableSectionHeading, definitionId: context.IteratorPath.CurrentPath()));
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                ret.AddRange(this.SwaggerApiViewGlobalParameters.TokenSerialize(context));
            }
            return ret.ToArray();
        }

        public NavigationItem[] BuildNavigationItems(IteratorPath iteratorPath = null)
        {
            iteratorPath ??= new IteratorPath();
            List<NavigationItem> ret = new List<NavigationItem>();

            ret.Add(this.SwaggerApiViewGeneral.BuildNavigationItem(iteratorPath));
            ret.Add(this.Paths.BuildNavigationItem(iteratorPath));
            if (this.SwaggerApiViewDefinitions != null && this.SwaggerApiViewDefinitions.Count > 0)
            {
                ret.Add(this.SwaggerApiViewDefinitions.BuildNavigationItem(iteratorPath));
            }

            if (this.SwaggerApiViewGlobalParameters != null && this.SwaggerApiViewGlobalParameters.Count > 0)
            {
                ret.Add(this.SwaggerApiViewGlobalParameters.BuildNavigationItem(iteratorPath));
            }

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
                PackageVersion = this.APIVersion,
                Navigation = new NavigationItem[] { this.BuildNavigationItem() }
            };
            return ret;
        }

        public NavigationItem BuildNavigationItem(IteratorPath iteratorPath = null)
        {
            iteratorPath ??= new IteratorPath();
            iteratorPath.Add(this.fileName);
            NavigationItem ret = new NavigationItem { Text = this.fileName, ChildItems = this.BuildNavigationItems(iteratorPath) };
            return ret;
        }
    }
}
