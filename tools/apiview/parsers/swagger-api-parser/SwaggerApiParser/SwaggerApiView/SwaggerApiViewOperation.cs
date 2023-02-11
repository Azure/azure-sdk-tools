using System;
using System.Collections.Generic;

namespace SwaggerApiParser;

public class SwaggerApiViewOperation : ITokenSerializable
{
    public string operationId { get; set; }
    public string operationIdPrefix;
    public string operationIdAction { get; set; }
    public string description { get; set; }
    
    public List<string> tags { get; set; }

    public List<string> procudes { get; set; }
    
    public List<string> consumes { get; set; }
    public string summary { get; set; }
    public string method { get; set; }
    public string path { get; set; }
    
    public XMsPageable xMSPageable { get; set; }

    public Boolean xMsLongRunningOperation { get; set; }
    
    public Operation operation { get; set; }

    public SwaggerApiViewOperationParameters PathParameters { get; set; }
    public SwaggerApiViewOperationParameters QueryParameters { get; set; }
    public SwaggerApiViewOperationParameters BodyParameters { get; set; }
    
    public SwaggerApiViewOperationParameters HeaderParameters { get; set; }

    public List<SwaggerApiViewResponse> Responses { get; set; }

    public string _iteratorPath;

    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();

        ret.Add(TokenSerializer.FoldableContentStart());
        
        if (this.description != null)
        {
            ret.Add(TokenSerializer.NavigableToken("description", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath("description")));
            ret.Add(TokenSerializer.Colon());
            ret.Add(new CodeFileToken(this.description, CodeFileTokenKind.Literal));
            ret.Add(TokenSerializer.NewLine());
        }
        
        if (this.summary != null)
        {
            ret.Add(TokenSerializer.NavigableToken("summary", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath("summary")));
            ret.Add(TokenSerializer.Colon());
            ret.Add(new CodeFileToken(this.summary, CodeFileTokenKind.Literal));
            ret.Add(TokenSerializer.NewLine());
        }

        ret.Add(TokenSerializer.NavigableToken("operationId", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath("Parameters")));
        ret.Add(TokenSerializer.Colon());
        ret.Add(new CodeFileToken(this.operationId, CodeFileTokenKind.TypeName));
        ret.Add(TokenSerializer.NewLine());

        if (tags != null)
        {
            ret.Add(TokenSerializer.NavigableToken("tags", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath("tags")));
            ret.Add(TokenSerializer.Colon());
            ret.Add(new CodeFileToken(string.Join(",", tags), CodeFileTokenKind.Literal));
            ret.Add(TokenSerializer.NewLine());
        }
    
        if (this.xMsLongRunningOperation)
        {
            ret.Add(TokenSerializer.NavigableToken("x-ms-long-running-operation", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath("x-ms-long-running-operation")));
            ret.Add(TokenSerializer.Colon());
            ret.Add(new CodeFileToken("true", CodeFileTokenKind.Literal));
            ret.Add(TokenSerializer.NewLine());
        }

        if (this.xMSPageable != null)
        {
            ret.Add(TokenSerializer.NavigableToken("x-ms-pageable", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath("x-ms-pageable")));
            ret.Add(TokenSerializer.Colon());
            ret.Add(new CodeFileToken(this.xMSPageable.nextLinkName, CodeFileTokenKind.Literal));
            ret.Add(TokenSerializer.NewLine());
        }

        if (this.procudes!=null)
        {
            ret.AddRange(TokenSerializer.KeyValueTokens("produces", string.Join(",", this.procudes)));
        }

        if (this.consumes != null)
        {
            ret.AddRange(TokenSerializer.KeyValueTokens("consumes", string.Join(",", this.consumes)));
        }

        // new line for `Parameters` section.
        ret.Add(TokenSerializer.NewLine());

        ret.AddRange(PathParameters.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath)));
        ret.AddRange(QueryParameters.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath)));
        ret.AddRange(BodyParameters.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath)));
        ret.AddRange(HeaderParameters.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath)));
        
        // new line for `Response` section.
        ret.Add(TokenSerializer.NewLine());


        ret.Add(TokenSerializer.NavigableToken("Responses", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath("Responses")));
        ret.Add(TokenSerializer.Colon());
        ret.Add(TokenSerializer.NewLine());

        context.IteratorPath.Add("Responses");
        foreach (var response in Responses)
        {
            ret.AddRange(response.TokenSerialize(new SerializeContext(context.intent + 1, context.IteratorPath)));
        }
        context.IteratorPath.Pop();

        ret.Add(TokenSerializer.NewLine());

        ret.Add(TokenSerializer.FoldableContentEnd());

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
