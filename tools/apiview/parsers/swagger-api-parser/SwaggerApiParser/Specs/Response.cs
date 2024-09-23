using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwaggerApiParser.Specs
{
    public class Response : Reference
    {
        public string description { get; set; }
        public Schema schema { get; set; }
        public Dictionary<string, Header> headers { get; set; }
        public Example examples { get; set; }
        [JsonExtensionData]
        public IDictionary<string, JsonElement> patternedObjects { get; set; }
    }

    public class Example : Dictionary<string, JsonElement>
    {
    }
}

