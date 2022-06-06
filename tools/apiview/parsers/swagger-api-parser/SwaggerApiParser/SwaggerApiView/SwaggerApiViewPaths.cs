using System.Collections.Generic;
using System.Linq;
using APIView;

namespace SwaggerApiParser;

public class SwaggerApiViewPaths : Dictionary<string, List<SwaggerApiViewOperation>>, INavigable, ITokenSerializable
{
    public void AddSwaggerApiViewOperation(SwaggerApiViewOperation op)
    {
        bool found = this.TryGetValue(op.operationIdPrefix, out var operations);
        if (found && operations != null)
        {
            operations.Add(op);
        }
        else
        {
            operations = new List<SwaggerApiViewOperation> {op};
            this.TryAdd(op.operationIdPrefix, operations);
        }
    }

    public void SortByMethod()
    {
        foreach (var key in this.Keys)
        {
            SwaggerApiViewOperationComp comp = new SwaggerApiViewOperationComp();
            this[key].Sort(comp);
        }
    }

    public NavigationItem BuildNavigationItem()
    {
        NavigationItem ret = new NavigationItem() {Text = "Paths", NavigationId = "Paths"};
        IteratorPath iteratorPath = new IteratorPath();
        iteratorPath.Add("Paths");

        List<NavigationItem> operationIdNavigations = new List<NavigationItem>();
        foreach (var path in this)
        {
            iteratorPath.Add(path.Key);
            NavigationItem operationIdNavigation = new NavigationItem() {Text = path.Key, NavigationId = iteratorPath.CurrentPath()};
            List<NavigationItem> operationIdActionNavigations = new List<NavigationItem>();

            var idx = 0;
            foreach (var operation in path.Value)
            {
                iteratorPath.Add(idx.ToString());
                iteratorPath.Add("operationId");
                iteratorPath.Add(operation.operationId);
                operationIdActionNavigations.Add(new NavigationItem() {Text = $"{operation.operationIdAction} - {operation.method}", NavigationId = iteratorPath.CurrentPath()});
                operation._iteratorPath = iteratorPath.CurrentPath();
                iteratorPath.Pop();
                iteratorPath.Pop();
                iteratorPath.Pop();
                idx++;
            }

            iteratorPath.Pop();

            operationIdNavigation.ChildItems = operationIdActionNavigations.ToArray();
            operationIdNavigations.Add(operationIdNavigation);
        }

        ret.ChildItems = operationIdNavigations.ToArray();
        return ret;
    }


    public Dictionary<string, string> GenerateOperationIdPathsMapping()
    {
        Dictionary<string, string> ret = new Dictionary<string, string>();
        foreach (var operation in this.Values.SelectMany(operations => operations))
        {
            ret.TryAdd(operation.operationId, operation._iteratorPath);
        }

        return ret;
    }

    public CodeFileToken[] TokenSerialize(int intent = 0)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        foreach (var (key, value) in this)
        {
            ret.Add(TokenSerializer.Intent(intent));
            ret.Add(new CodeFileToken(key, CodeFileTokenKind.Keyword));
            ret.Add(new CodeFileToken(":", CodeFileTokenKind.Punctuation));
            ret.Add(TokenSerializer.NewLine());
            foreach (var operation in value)
            {
                ret.Add(TokenSerializer.Intent(intent + 1));
                var methodPath = $"{operation.method} - {operation.path}";
                ret.Add(new CodeFileToken(methodPath, CodeFileTokenKind.Literal));
                ret.Add(new CodeFileToken(":", CodeFileTokenKind.Punctuation));
                ret.Add(TokenSerializer.NewLine());
                
                // collapse operation here.
                ret.AddRange(operation.TokenSerialize(intent + 2));
            }
        }

        return ret.ToArray();
    }
}
