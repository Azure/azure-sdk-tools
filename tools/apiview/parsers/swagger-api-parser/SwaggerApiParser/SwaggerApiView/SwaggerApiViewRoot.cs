using System;
using System.Collections.Generic;
using System.IO;

namespace SwaggerApiParser;

public class SwaggerApiViewRoot : ITokenSerializable
{
    public String ResourceProvider;
    public String PackageName;
    public Dictionary<String, SwaggerApiViewSpec> SwaggerApiViewSpecs;
    public SchemaCache schemaCache;

    public SwaggerApiViewRoot(string resourceProvider, string packageName)
    {
        this.ResourceProvider = resourceProvider;
        this.PackageName = packageName;
        this.SwaggerApiViewSpecs = new Dictionary<string, SwaggerApiViewSpec>();
        this.schemaCache = new SchemaCache();
    }

    public void AddSwaggerSpec(SwaggerSpec swaggerSpec, string swaggerFilePath, string resourceProvider = "", string swaggerLink="")
    {
        var swaggerApiViewSpec = SwaggerApiViewGenerator.GenerateSwaggerApiView(swaggerSpec, swaggerFilePath, this.schemaCache, resourceProvider, swaggerLink);

        if (swaggerApiViewSpec != null)
        {
            this.SwaggerApiViewSpecs.Add(swaggerFilePath, swaggerApiViewSpec);
        }
    }

    public void AddDefinitionToCache(SwaggerSpec swaggerSpec, string swaggerFilePath)
    {
        SwaggerApiViewGenerator.AddDefinitionsToCache(swaggerSpec, swaggerFilePath, this.schemaCache);
    }

    public CodeFile GenerateCodeFile()
    {
        SerializeContext context = new SerializeContext();
        CodeFile ret = new CodeFile()
        {
            Tokens = this.TokenSerialize(context),
            Language = "Swagger",
            VersionString = "0",
            Name = this.ResourceProvider,
            PackageName = this.PackageName,
            Navigation = this.BuildNavigationItems()
        };

        return ret;
    }


    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        foreach (var kv in this.SwaggerApiViewSpecs)
        {
            // ret.Add(TokenSerializer.Intent(context.intent));
            var fileName = Path.GetFileName(kv.Key);
            ret.Add(new CodeFileToken(fileName, CodeFileTokenKind.Keyword));
            ret.Add(TokenSerializer.Colon());
            ret.Add(TokenSerializer.NewLine());

            var specContext = new SerializeContext(context.intent + 1, context.IteratorPath);
            specContext.IteratorPath.Add(fileName);

            var specToken = kv.Value.TokenSerialize(specContext);

            ret.AddRange(specToken);
            ret.Add(TokenSerializer.NewLine());
        }

        return ret.ToArray();
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
