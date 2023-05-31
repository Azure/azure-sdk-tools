using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser.Specs
{
    public class Tag : ITokenSerializable
    {
        public string name { get; set; }
        public string description { get; set; }
        public ExternalDocs externalDocs { get; set; }
        [JsonExtensionData]
        public IDictionary<string, dynamic> patternedObjects { get; set; }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            return TokenSerializer.TokenSerialize(this, context);
        }
    }
}
