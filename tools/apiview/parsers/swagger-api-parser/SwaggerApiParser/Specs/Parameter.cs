using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwaggerApiParser.Specs
{
    public class Parameter : Items
    {
        public string name { get; set; }
        [JsonPropertyName("in")]
        public string @in { get; set; }
        public string description { get; set; }
        public bool required { get; set; }
        public Schema schema { get; set; }
        public bool allowEmptyValue { get; set; }
    }
}
