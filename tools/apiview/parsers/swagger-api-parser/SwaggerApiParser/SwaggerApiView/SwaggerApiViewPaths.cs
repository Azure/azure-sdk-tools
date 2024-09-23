using System.Collections.Generic;
using System.Linq;

namespace SwaggerApiParser.SwaggerApiView
{
    public class SwaggerApiViewPaths : SortedDictionary<string, List<SwaggerApiViewOperation>>, INavigable, ITokenSerializable
    {
        public void AddSwaggerApiViewOperation(SwaggerApiViewOperation op)
        {
            bool found = this.TryGetValue(op.path, out var operations);
            if (found && operations != null)
            {
                operations.Add(op);
            }
            else
            {
                operations = new List<SwaggerApiViewOperation> { op };
                this.TryAdd(op.path, operations);
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

        public NavigationItem BuildNavigationItem(IteratorPath iteratorPath = null)
        {
            iteratorPath ??= new IteratorPath();

            iteratorPath.Add("Paths");
            NavigationItem ret = new NavigationItem() { Text = "Paths", NavigationId = iteratorPath.CurrentPath() };

            List<NavigationItem> operationIdNavigations = new List<NavigationItem>();
            foreach (var path in this)
            {
                iteratorPath.Add(path.Key);
                NavigationItem operationIdNavigation = new NavigationItem() { Text = path.Key, NavigationId = iteratorPath.CurrentPath() };
                List<NavigationItem> operationIdActionNavigations = new List<NavigationItem>();

                var idx = 0;
                foreach (var operation in path.Value)
                {
                    iteratorPath.Add(idx.ToString());
                    iteratorPath.Add("operationId");
                    iteratorPath.Add(operation.operationId);
                    operationIdActionNavigations.Add(new NavigationItem() { Text = $"{operation.operationIdAction} - {operation.method}", NavigationId = iteratorPath.CurrentPath() });
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
            iteratorPath.Pop();
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

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            ret.Add(TokenSerializer.FoldableContentStart());
            foreach (var (key, value) in this)
            {
                context.IteratorPath.Add(key);
                ret.Add(TokenSerializer.NavigableToken(key, CodeFileTokenKind.FoldableSectionHeading, context.IteratorPath.CurrentPath()));
                ret.Add(TokenSerializer.NewLine());

                ret.Add(TokenSerializer.FoldableContentStart());
                var idx = 0;
                foreach (var operation in value)
                {
                    context.IteratorPath.AddRange(new List<string> { idx.ToString(), "operationId", operation.operationId });

                    ret.Add(TokenSerializer.NavigableToken($"{operation.method.ToUpper()}", CodeFileTokenKind.FoldableSectionHeading, context.IteratorPath.CurrentPath()));
                    ret.Add(TokenSerializer.NewLine());

                    ret.AddRange(operation.TokenSerialize(new SerializeContext(context.indent + 2, context.IteratorPath, context.definitionsNames)));

                    context.IteratorPath.PopMulti(3);
                    idx += 1;
                }
                ret.Add(TokenSerializer.FoldableContentEnd());

                context.IteratorPath.Pop();
            }
            ret.Add(TokenSerializer.FoldableContentEnd());
            return ret.ToArray();
        }
    }
}
