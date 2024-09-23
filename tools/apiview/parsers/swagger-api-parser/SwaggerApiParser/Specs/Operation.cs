using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwaggerApiParser.Specs
{
    public class Operation
    {
        public List<string> tags { get; set; }
        public string summary { get; set; }
        public string description { get; set; }
        public ExternalDocs externalDocs { get; set; }
        public string operationId { get; set; }
        public List<string> consumes { get; set; }
        public List<string> produces { get; set; }
        public List<Parameter> parameters { get; set; }
        public Dictionary<string, Response> responses { get; set; }
        public List<string> schemes { get; set; }
        public bool deprecated { get; set; }
        public List<Security> security { get; set; }
        [JsonExtensionData]
        public IDictionary<string, JsonElement> patternedObjects { get; set; }
    }
}


