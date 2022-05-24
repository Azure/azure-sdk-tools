using System.Collections.Generic;

namespace swagger_api_parser
{
    public class SwaggerApiViewSpec
    {
        public General General;
        public Dictionary<string, List<SwaggerAPIViewOperation>> Paths;

        public SwaggerApiViewSpec()
        {
            this.Paths = new Dictionary<string, List<SwaggerAPIViewOperation>>();
            this.General = new General();
        }

        public void AddSwaggerApiViewOperation(SwaggerAPIViewOperation op)
        {
            bool found = this.Paths.TryGetValue(op.operationIdPrefix, out var operations);
            if (found && operations != null)
            {
                operations.Add(op);
            }
            else
            {
                operations = new List<SwaggerAPIViewOperation> {op};
                this.Paths.TryAdd(op.operationIdPrefix, operations);
            }
        }
    }

    public class General
    {
        public string swagger { set; get; }
        public Info info { set; get; }
    }

    public class SwaggerAPIViewOperation
    {
        public string operationId;
        public string operationIdPrefix;
        public string operationIdAction;
        public string method;
        public string path;
        public Operation operation;
        
        // sorted by method.
    }
}
