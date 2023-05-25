using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwaggerApiParser.Specs
{
    public class Response
    {
        public string description { get; set; }
        public BaseSchema schema { get; set; }
        public Dictionary<string, Header> headers { get; set; }
        


        [JsonExtensionData] public IDictionary<string, object> examples { get; set; }
    }
}

