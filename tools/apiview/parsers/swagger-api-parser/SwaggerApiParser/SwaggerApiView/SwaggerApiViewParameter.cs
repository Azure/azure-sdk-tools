using System.Collections.Generic;
using APIView;

namespace SwaggerApiParser;

public class SwaggerApiViewParameter : ITokenSerializable
{
    public string name { get; set; }
    public bool required { get; set; }
    public string description { get; set; }

    public string format { get; set; }
    public string type { get; set; }
    public string In { get; set; }

    public string Ref { get; set; }

    public BaseSchema schema { get; set; }


    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        if (this.IsRefObj())
        {
            return TokenSerializer.TokenSerialize(this, context.intent, new string[] {"Ref"});
        }

        // ret.Add(TokenSerializer.Intent(context.intent));
        ret.Add(new CodeFileToken(name, CodeFileTokenKind.Literal));
        ret.Add(TokenSerializer.Colon());
        ret.Add(new CodeFileToken(this.type, CodeFileTokenKind.Keyword));
        ret.Add(TokenSerializer.NewLine());
        if (this.schema != null)
        {
            ret.AddRange(this.schema.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath)));
        }

        return ret.ToArray();
    }

    public bool IsRefObj()
    {
        return this.Ref != null;
    }
}

public class SwaggerApiViewOperationParameters : List<SwaggerApiViewParameter>, ITokenSerializable
{
    private readonly string type;

    public SwaggerApiViewOperationParameters(string type)
    {
        this.type = type;
    }

    public CodeFileToken[] TokenSerializeYaml(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        if (this.Count == 0)
        {
            return ret.ToArray();
        }

        // ret.Add(TokenSerializer.Intent(context.intent));
        ret.Add(TokenSerializer.NavigableToken(this.type, CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath(this.type)));
        ret.Add(TokenSerializer.Colon());
        ret.Add(TokenSerializer.NewLine());

        // ret.Add(TokenSerializer.FoldableContentStart());
        foreach (var parameter in this)
        {
            ret.AddRange(parameter.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath)));
        }
        // ret.Add(TokenSerializer.FoldableContentEnd());

        return ret.ToArray();
    }

    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        if (this.Count == 0)
        {
            return System.Array.Empty<CodeFileToken>();
        }

        // ret.Add(TokenSerializer.Intent(context.intent));
        ret.Add(TokenSerializer.NavigableToken(this.type, CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath(this.type)));
        ret.Add(TokenSerializer.Colon());
        ret.Add(TokenSerializer.NewLine());
        string[] columns = new[] {"Name", "Type/Format", "Required", "Description"};
        var tableRows = this.TokenSerializeTableRows(context);
        ret.AddRange(TokenSerializer.TokenSerializeAsTableFormat(this.Count, 4, columns, tableRows));
        ret.Add(TokenSerializer.NewLine());
        return ret.ToArray();
    }

    private CodeFileToken[] TokenSerializeTableRows(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        foreach (var parameter in this)
        {
            ret.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(parameter.name, CodeFileTokenKind.MemberName)}));
            var parameterType = parameter.type;

            if (parameter.format != null)
            {
                parameterType += "/" + parameter.format;
            }

            ret.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(parameterType, CodeFileTokenKind.Keyword)}));
            var required = parameter.required;
            ret.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(required.ToString(), CodeFileTokenKind.Literal)}));
            ret.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(parameter.description, CodeFileTokenKind.Literal)}));
        }

        return ret.ToArray();
    }
}
