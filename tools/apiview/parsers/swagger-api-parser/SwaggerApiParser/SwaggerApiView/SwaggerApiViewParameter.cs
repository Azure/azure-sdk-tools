using System.Collections.Generic;
using APIView;

namespace SwaggerApiParser;

public class SwaggerApiViewParameter : ITokenSerializable
{
    public string name { get; set; }
    public bool required { get; set; }
    public string description { get; set; }
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

        ret.Add(TokenSerializer.Intent(context.intent));
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

    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        if (this.Count == 0)
        {
            return ret.ToArray();
        }

        ret.Add(TokenSerializer.Intent(context.intent));
        ret.Add(new CodeFileToken(this.type, CodeFileTokenKind.Keyword));
        ret.Add(TokenSerializer.Colon());
        ret.Add(TokenSerializer.NewLine());
        foreach (var parameter in this)
        {
            ret.AddRange(parameter.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath)));
        }

        return ret.ToArray();
    }
}
