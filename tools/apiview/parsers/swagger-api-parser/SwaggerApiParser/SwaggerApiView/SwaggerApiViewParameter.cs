using System;
using System.Collections.Generic;
using APIView;
using Microsoft.VisualBasic;

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

    public List<string> GetKeywords()
    {
        List<string> ret = new List<string>();
        if (this.required)
        {
            ret.Add("required");
        }

        return ret;
    }

    public String GetTypeFormat()
    {
        var ret = "";
        if (this.type != null)
        {
            ret = this.type;
        }
        else if (this.schema != null)
        {
            ret = this.schema.GetTypeFormat();
        }

        if (this.format != null)
        {
            ret += $"/{this.format}";
        }

        return ret;
    }


    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        if (this.IsRefObj())
        {
            ret.Add(TokenSerializer.NavigableToken("ref", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath("ref")));
            ret.Add(TokenSerializer.Colon());
            string navigationToId = context.IteratorPath.rootPath() + Utils.GetRefDefinitionIdPath(this.Ref);
            ret.Add(new CodeFileToken(this.Ref, CodeFileTokenKind.Literal) {NavigateToId = navigationToId});
            return ret.ToArray();
        }

        // ret.Add(TokenSerializer.Intent(context.intent));

        string[] columns = new[] {"name", "Type/Format", "In", "Keywords", "Description"};

        List<CodeFileToken> tableRows = new List<CodeFileToken>();
        tableRows.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(this.name, CodeFileTokenKind.Literal)}));
        var parameterType = this.type;
        if (this.format != null)
        {
            parameterType += "/" + format;
        }

        tableRows.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(parameterType, CodeFileTokenKind.Literal)}));
        tableRows.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(this.In, CodeFileTokenKind.Literal)}));
        tableRows.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(String.Join(",", this.GetKeywords()), CodeFileTokenKind.Literal)}));
        tableRows.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(this.description, CodeFileTokenKind.Literal)}));
        ret.AddRange(TokenSerializer.TokenSerializeAsTableFormat(1, 5, columns, tableRows.ToArray(), context.IteratorPath.CurrentNextPath("table")));
        ret.Add(TokenSerializer.NewLine());
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
            // ret.AddRange(parameter.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath)));
        }

        ret.Add(TokenSerializer.FoldableContentEnd());

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
        context.IteratorPath.Add(this.type);
        ret.Add(TokenSerializer.NavigableToken(this.type, CodeFileTokenKind.Keyword, context.IteratorPath.CurrentPath()));
        ret.Add(TokenSerializer.Colon());
        ret.Add(TokenSerializer.NewLine());
        string[] columns = new[] {"Name", "Type/Format", "Keywords", "Description"};
        var tableRows = this.TokenSerializeTableRows(context);
        ret.AddRange(TokenSerializer.TokenSerializeAsTableFormat(this.Count, 4, columns, tableRows, context.IteratorPath.CurrentNextPath("table")));
        ret.Add(TokenSerializer.NewLine());
        context.IteratorPath.Pop();
        return ret.ToArray();
    }

    private CodeFileToken[] TokenSerializeTableRows(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        foreach (var parameter in this)
        {
            ret.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(parameter.name, CodeFileTokenKind.MemberName)}));
            var parameterType = parameter.GetTypeFormat();


            ret.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(parameterType, CodeFileTokenKind.Keyword)}));
            ret.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(String.Join(",", parameter.GetKeywords()), CodeFileTokenKind.Literal)}));
            ret.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(parameter.description, CodeFileTokenKind.Literal)}));
        }

        return ret.ToArray();
    }
}
