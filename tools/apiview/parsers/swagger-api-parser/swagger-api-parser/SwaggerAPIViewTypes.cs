using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using APIView;


namespace swagger_api_parser
{
    public class SwaggerApiViewSpec
    {
        public General General { get; set; }
        public SwaggerApiViewPaths Paths { get; set; }

        public SwaggerApiViewOperations Operations { get; set; }


        public SwaggerApiViewSpec()
        {
            this.Paths = new SwaggerApiViewPaths();
            this.General = new General();
            this.Operations = new SwaggerApiViewOperations();
        }

        public CodeFileToken[] TokenSerialize()
        {
            var generalTokens = this.General.TokenSerialize();
            CodeFileToken[] ret = new CodeFileToken[] { };
            return generalTokens;
        }

        public NavigationItem[] BuildNavigationItems()
        {
            List<NavigationItem> ret = new List<NavigationItem>();
            ret.Add(this.General.BuildNavigationItem());
            ret.Add(this.Paths.BuildNavigationItem());
            ret.Add(this.Operations.BuildNavigationItem());
            return ret.ToArray();
        }
    }

    public class General
    {
        public string swagger { set; get; }
        public Info info { set; get; }

        public string host { get; set; }
        public List<string> schemes { get; set; }
        public List<string> consumes { get; set; }
        public List<string> produces { get; set; }

        public General()
        {
            this.info = new Info();
        }

        public CodeFileToken[] TokenSerialize()
        {
            var jsonDoc = JsonSerializer.SerializeToDocument(this);
            return Visitor.GenerateCodeFileTokens(jsonDoc);
        }

        public NavigationItem BuildNavigationItem()
        {
            return new NavigationItem() {Text = "General", NavigationId = "General"};
        }
    }

    public class SwaggerApiViewPaths : Dictionary<string, List<SwaggerApiViewOperation>>
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
                    operationIdActionNavigations.Add(new NavigationItem() {Text = operation.operationIdAction, NavigationId = iteratorPath.CurrentPath()});
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
    }

    public class SwaggerApiViewOperation
    {
        public string operationId { get; set; }
        public string operationIdPrefix;
        public string operationIdAction { get; set; }
        public string method { get; set; }
        public string path { get; set; }
        [JsonIgnore] public Operation operation { get; set; }


        // sorted by method.
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
                {"delete", 5},
            };
            

            priority.TryGetValue(a.method, out var priorityA);
            priority.TryGetValue(b.method, out var priorityB);
            if (priorityA == priorityB)
            {
                return 0;
            }
            
            if(priorityA<priorityB)
            {
                return -1;
            }
            return 1;
        }
    }

    public class SwaggerApiViewOperations : List<Operation>
    {
        public NavigationItem BuildNavigationItem()
        {
            NavigationItem ret = new NavigationItem() {Text = "Operations"};
            List<NavigationItem> children = new List<NavigationItem>();

            foreach (var operation in this)
            {
                children.Add(new NavigationItem() {Text = operation.operationId, NavigationId = operation.operationId});
            }

            ret.ChildItems = children.ToArray();
            return ret;
        }
    }

    public class SwaggerApiViewDefinitions : List<Definition>
    {
        public NavigationItem BuildNavigationItem()
        {
            NavigationItem ret = new NavigationItem() {Text = "Definitions"};
            List<NavigationItem> children = new List<NavigationItem>();

            return ret;
        }
    }
}
