using System;
using System.Collections.Generic;
using APIView;

namespace SwaggerApiParser;

public class SwaggerApiViewOperation : ITokenSerializable
{
    public string operationId { get; set; }
    public string operationIdPrefix;
    public string operationIdAction { get; set; }
    public string description { get; set; }
    public string method { get; set; }
    public string path { get; set; }

    public Boolean xMsLongRunningOperation { get; set; }
    public Operation operation { get; set; }

    public SwaggerApiViewOperationParameters PathParameters { get; set; }
    public SwaggerApiViewOperationParameters QueryParameters { get; set; }
    public SwaggerApiViewOperationParameters BodyParameters { get; set; }

    public List<SwaggerApiViewResponse> Responses { get; set; }

    public string _iteratorPath;

    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        ret.Add(TokenSerializer.Intent(context.intent));
        ret.Add(new CodeFileToken(this.operationIdAction, CodeFileTokenKind.TypeName));
        ret.Add(TokenSerializer.NewLine());


        ret.Add(TokenSerializer.Intent(context.intent));
        ret.Add(new CodeFileToken(this.description, CodeFileTokenKind.Literal));
        ret.Add(TokenSerializer.NewLine());

        if (this.xMsLongRunningOperation)
        {
            ret.Add(TokenSerializer.Intent(context.intent));
            ret.Add(new CodeFileToken("x-ms-long-running-operation", CodeFileTokenKind.Keyword));
            ret.Add(TokenSerializer.Colon());
            ret.Add(new CodeFileToken("true", CodeFileTokenKind.Literal));
            ret.Add(TokenSerializer.NewLine());
        }

        ret.Add(TokenSerializer.Intent(context.intent));
        ret.Add(new CodeFileToken("Parameters", CodeFileTokenKind.Keyword));
        ret.Add(TokenSerializer.Colon());
        ret.Add(TokenSerializer.NewLine());

        ret.AddRange(PathParameters.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath)));
        ret.AddRange(QueryParameters.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath)));
        ret.AddRange(BodyParameters.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath)));


        ret.Add(TokenSerializer.Intent(context.intent));
        ret.Add(new CodeFileToken("Responses", CodeFileTokenKind.Keyword));
        ret.Add(TokenSerializer.Colon());
        ret.Add(TokenSerializer.NewLine());

        foreach (var response in Responses)
        {
            ret.AddRange(response.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath)));
        }

        return ret.ToArray();
    }
}

public class SwaggerApiViewOperationComp : IComparer<SwaggerApiViewOperation>
{
    public int Compare(SwaggerApiViewOperation a, SwaggerApiViewOperation b)
    {
        Dictionary<string, int> priority = new Dictionary<string, int>()
        {
            {"post", 1},
            {"put", 2},
            {"patch", 3},
            {"get", 4},
            {"get-action", 5},
            {"delete", 6},
            {"post-action", 7}
        };


        priority.TryGetValue(GetMethodType(a), out var priorityA);
        priority.TryGetValue(GetMethodType(b), out var priorityB);
        if (priorityA == priorityB)
        {
            return 0;
        }

        if (priorityA < priorityB)
        {
            return -1;
        }

        return 1;
    }

    /*
     * Post:  /<service>/<resource-collection>/<resource-id>. CreateOrUpdate
     * Post action:  /<service>/<resource-collection>/<resource-id>:<action>. PostAction
     */
    private static string GetMethodType(SwaggerApiViewOperation operation)
    {
        return operation.method switch
        {
            "post" => operation.path == null || operation.path.Split("/").Length % 2 == 1 ? "post" : "post-action",
            "get" => operation.path == null || operation.path.Split("/").Length % 2 == 1 ? "get" : "get-action",
            _ => operation.method
        };
    }
}
