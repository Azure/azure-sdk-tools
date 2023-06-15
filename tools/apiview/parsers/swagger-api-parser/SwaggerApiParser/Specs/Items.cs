using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser.Specs
{
    public class Items : Reference
    {
        public string type { get; set; }
        public string format { get; set; }
        public Items items { get; set; }
        public string collectionFormat { get; set; }
        public JsonElement @default { get; set; }
        public double? maximum { get; set; }
        public bool? exclusiveMaximum { get; set; }
        public double? minimum { get; set; }
        public bool? exclusiveMinimum { get; set; }
        public int? maxLength { get; set; }
        public int? minLength { get; set; }
        public string pattern { get; set; }
        public int? maxItems { get; set; }
        public int? minItems { get; set; }
        public bool? uniqueItems { get; set; }
        public List<dynamic> @enum { get; set; }
        public double? multipleOf { get; set; }
        [JsonExtensionData]
        public IDictionary<string, JsonElement> patternedObjects { get; set; }

        public List<string> GetKeywords()
        {
            List<string> keywords = new List<string>();
            if (!string.IsNullOrEmpty(this.collectionFormat))
                keywords.Add($"collectionFormat : {this.collectionFormat}");

            if (@default.ValueKind == JsonValueKind.String)
                keywords.Add($"default : {this.@default.ToString()}");

            if (this.maximum != null)
                keywords.Add($"maximum : {this.maximum}");

            if (this.exclusiveMaximum != null)
                keywords.Add($"exclusiveMaximum : {this.exclusiveMaximum}");

            if (this.minimum != null)
                keywords.Add($"minimum : {this.minimum}");

            if (this.exclusiveMinimum != null)
                keywords.Add($"exclusiveMinimum : {this.exclusiveMinimum}");

            if (this.maxLength != null)
                keywords.Add($"maxLength : {this.maxLength}");

            if (this.minLength != null)
                keywords.Add($"minLength : {this.minLength}");

            if (!string.IsNullOrEmpty(this.pattern))
                keywords.Add($"pattern : {this.pattern}");

            if (this.maxItems != null)
                keywords.Add($"maxItems : {this.maxItems}");

            if (this.uniqueItems != null)
                keywords.Add($"uniqueItems : {this.uniqueItems}");

            if (this.@enum != null && this.@enum.Count > 0)
                keywords.Add($"enum: [{string.Join(", ", this.@enum)}]");

            return keywords;
        }
    }
}
