using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwaggerApiParser;

public class Operation
{
    public string description { get; set; }
    public string operationId { get; set; }
    
    [JsonPropertyName("x-ms-long-running-operation")]
    public Boolean xMsLongRunningOperaion { get; set; }

    public List<Parameter> parameters { get; set; }

    public Dictionary<string, Response> responses { get; set; }
        
}
