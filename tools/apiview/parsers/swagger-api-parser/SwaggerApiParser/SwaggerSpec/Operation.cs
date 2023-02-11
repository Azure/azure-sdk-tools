using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwaggerApiParser;

public class Operation
{
    public string description { get; set; }
    
    public string summary { get; set; }
    public string operationId { get; set; }
    
    public List<string> tags { get; set; }
    
    public List<string> produces { get; set; }
    
    public List<string> consumes { get; set; }

    [JsonPropertyName("x-ms-long-running-operation")]
    public Boolean xMsLongRunningOperaion { get; set; }
    
    [JsonPropertyName("x-ms-pageable")]
    public XMsPageable xMsPageable { get; set; }

    public List<Parameter> parameters { get; set; }

    public Dictionary<string, Response> responses { get; set; }
        
}

public class XMsPageable
{
    public string itemName { get; set; }
    public string nextLinkName { get; set; }
    public string operationName { get; set; }
}