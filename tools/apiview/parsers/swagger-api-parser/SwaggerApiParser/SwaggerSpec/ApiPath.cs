using System.Collections.Generic;

namespace SwaggerApiParser;

public class ApiPath
{
    public List<Parameter> parameters { get; set; }
    public Operation get { get; set; }
    public Operation put { get; set; }
    public Operation post { get; set; }
    public Operation delete { get; set; }
    public Operation options { get; set; }
    public Operation head { get; set; }
    public Operation patch { get; set; }
    
   
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
            var operationMethods = new List<string> { "get", "put", "post", "delete", "options", "head", "patch" };
            foreach (var method in operationMethods)
            {
                if (this[method] is Operation operation)
                {
                    if (this.parameters != null)
                    {
                        operation.parameters.AddRange(this.parameters);
                    }   
                    ret.Add(method, operation);
                }
            }
            return ret;
        }
    }
}
