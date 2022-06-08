using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwaggerApiParser
{
    public class Header : BaseSchema
    {
    }

    public class Definition : BaseSchema
    {
    }


    public class BaseSchema
    {
        public string type { get; set; }
        public string description { get; set; }
        public string format { get; set; }

        public List<BaseSchema> allOf { get; set; }
        public List<BaseSchema> anyOf { get; set; }
        public List<BaseSchema> oneOf { get; set; }

        // public Boolean additionalProperties { get; set; }
        public bool readOnly { get; set; }
        public string discriminator { get; set; }
        public Dictionary<string, BaseSchema> properties { get; set; }

        [JsonPropertyName("x-ms-discriminator-value")]
        public string xMsDiscriminatorValue { get; set; }

        public List<string> required { get; set; }


        [JsonPropertyName("$ref")] public string Ref { get; set; }


        // public List<BaseSchema> items { get; set; }

        public bool IsRefObj()
        {
            return this.Ref != null;
        }
    }
}
