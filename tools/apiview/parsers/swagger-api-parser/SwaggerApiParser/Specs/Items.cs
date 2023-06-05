using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwaggerApiParser.Specs
{
    public class Items : Reference
    {
        public string type { get; set; }
        public string format { get; set; }
        public Items items { get; set; }
        public string collectionFormat { get; set; }
        public dynamic @default { get; set; }
        public double maximum { get; set; }
        public bool? exclusiveMaximum { get; set; }
        public double minimum { get; set; }
        public bool? exclusiveMinimum { get; set; }
        public int? maxLength { get; set; }
        public int? minLength { get; set; }
        public string pattern { get; set; }
        public int? maxItems { get; set; }
        public int? minItems { get; set; }
        public bool? uniqueItems { get; set; }
        public List<dynamic> @enum { get; set; }
        public int? multipleOf { get; set; }
        [JsonExtensionData]
        public IDictionary<string, JsonElement> patternedObjects { get; set; }
    }
}
