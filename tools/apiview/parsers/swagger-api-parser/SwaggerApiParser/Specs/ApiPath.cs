using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwaggerApiParser.Specs
{
    public class ApiPath
    {
        [JsonPropertyName("$ref")]
        public string @ref { get; set; }
        public Operation get { get; set; }
        public Operation put { get; set; }
        public Operation post { get; set; }
        public Operation delete { get; set; }
        public Operation options { get; set; }
        public Operation head { get; set; }
        public Operation patch { get; set; }
        public List<Parameter> parameters { get; set; }
        [JsonExtensionData]
        public IDictionary<string, JsonElement> patternedObjects { get; set; }

        private static readonly List<string> operationMethods = new List<string>() { "get", "put", "post", "delete", "options", "head", "patch" };

        public object this[string propertyName]
        {
            get { return this.GetType().GetProperty(propertyName)?.GetValue(this, null); }
            set { this.GetType().GetProperty(propertyName)?.SetValue(this, value, null); }
        }

        public Dictionary<string, Operation> operations
        {
            get
            {
                var ret = new Dictionary<string, Operation>();
                foreach (var method in operationMethods)
                {
                    if (this[method] is Operation operation)
                    {
                        if (this.parameters != null)
                        {
                            if (operation.parameters == null)
                            {
                                operation.parameters = new List<Parameter>();
                            }

                            operation.parameters.AddRange(this.parameters);
                        }
                        ret.Add(method, operation);
                    }
                }
                return ret;
            }
        }
    }
}

