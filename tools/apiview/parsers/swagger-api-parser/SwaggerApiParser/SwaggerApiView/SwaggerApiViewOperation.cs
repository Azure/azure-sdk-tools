using System.Collections.Generic;
using APIView;

namespace SwaggerApiParser;

public class SwaggerApiViewOperation : ITokenSerializable
{
    public string operationId { get; set; }
    public string operationIdPrefix;
    public string operationIdAction { get; set; }
    public string method { get; set; }
    public string path { get; set; }
    public Operation operation { get; set; }

    public string _iteratorPath;

    public CodeFileToken[] TokenSerialize(int intent = 0)
    {
        return TokenSerializer.TokenSerialize(this, intent, new string[] { });
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
            "post" => operation.path.Split("/").Length % 2 == 1 ? "post" : "post-action",
            "get" => operation.path.Split("/").Length % 2 == 1 ? "get" : "get-action",
            _ => operation.method
        };
    }
}
