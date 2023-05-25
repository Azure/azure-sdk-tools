using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace SwaggerApiParser.Specs
{
    public class Schema : ITokenSerializable
    {
        [JsonPropertyName("$ref")]
        public string @ref { get; set; }
        public string format { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        [JsonPropertyName("default")]
        public dynamic @default { get; set; }
        public string multipleOf { get; set; }
        public string maximum { get; set; }
        public bool exclusiveMaximum { get; set; }
        public string minimum { get; set; }
        public bool exclusiveMinimum { get; set; }
        public int maxLength { get; set; }
        public int minLength { get; set; }
        public string pattern { get; set; }
        public int maxItems { get; set; }
        public int minItems { get; set; }
        public bool uniqueItems { get; set; }
        public int maxProperties { get; set; }
        public int minProperties { get; set; }
        public List<string> required { get; set; }
        [JsonPropertyName("enum")]
        public List<JsonElement> @enum { get; set; }
        public List<string> type { get; set; }
        public Schema items { get; set; } // Should this be an array?
        public List<Schema> allOf { get; set; }
        public Dictionary<string, Schema> properties { get; set; }
        public Schema additionalProperties { get; set; }
        public string discriminator { get; set; }
        public bool readOnly { get; set; }
        public XML xml { get; set; }
        public ExternalDocs externalDocs { get; set; }
        public dynamic example { get; set; }
        [JsonExtensionData]
        public IDictionary<string, dynamic> patternedObjects { get; set; }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            
        }
    }
}
